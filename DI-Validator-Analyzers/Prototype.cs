using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DI_Validator_Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DependencyInjectionRegistrationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DI001";
        private const string Category = "Dependency Injection";
        private static readonly LocalizableString Title = "Missing Dependency Injection Registration";
        private static readonly LocalizableString MessageFormat = "Type '{0}' is used in constructor but appears to be missing from DI registration.";
        private static readonly LocalizableString Description = "Type '{0}' used in constructor is not registered with dependency injection.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        // the list of registration methods that we will recognize
        private static readonly HashSet<string> RegistrationMethods = new HashSet<string>
        {
            "AddSingleton", "AddScoped", "AddTransient",
            "TryAddSingleton", "TryAddScoped", "TryAddTransient"
        };

        private class AnalysisData
        {
            public HashSet<string> RegisteredServices { get; } = new HashSet<string>();
            public List<ControllerConstructorInfo> ControllerConstructors { get; } = new List<ControllerConstructorInfo>();
        }

        private class ControllerConstructorInfo
        {
            public ConstructorDeclarationSyntax Constructor { get; }
            public INamedTypeSymbol ClassSymbol { get; }
            public SemanticModel SemanticModel { get; }

            public ControllerConstructorInfo(
                ConstructorDeclarationSyntax constructor,
                INamedTypeSymbol classSymbol,
                SemanticModel semanticModel)
            {
                Constructor = constructor;
                ClassSymbol = classSymbol;
                SemanticModel = semanticModel;
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Use a two-phase approach to solve the race condition
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var analysisData = new AnalysisData();

                // Phase 1: Collect all DI registrations
                compilationStartContext.RegisterSyntaxNodeAction(
                    ctx => CollectRegisteredTypes(ctx, analysisData.RegisteredServices),
                    SyntaxKind.InvocationExpression);

                // Phase 1: Also collect all controller constructors (but don't analyze them yet)
                compilationStartContext.RegisterSyntaxNodeAction(
                    ctx => CollectControllerConstructors(ctx, analysisData),
                    SyntaxKind.ConstructorDeclaration);

                // Phase 2: After all files have been processed, analyze the controller constructors
                compilationStartContext.RegisterCompilationEndAction(ctx =>
                {
                    // Debug output of registered types
                    Console.WriteLine("\n--------- REGISTERED TYPES ---------");
                    foreach (var type in analysisData.RegisteredServices.OrderBy(t => t))
                    {
                        Console.WriteLine($"[DI Debug] Registered: {type}");
                    }
                    Console.WriteLine("-----------------------------------\n");

                    // Now analyze all collected controller constructors
                    foreach (var controllerInfo in analysisData.ControllerConstructors)
                    {
                        AnalyzeControllerConstructor(ctx, controllerInfo, analysisData.RegisteredServices);
                    }
                });
            });
        }

        private void CollectRegisteredTypes(SyntaxNodeAnalysisContext context, HashSet<string> registeredServices)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // check if the call has member access
            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return;

            var methodName = memberAccess.Name.Identifier.Text;

            // check if it is a DI registration method from the list
            if (!RegistrationMethods.Contains(methodName))
                return;

            Console.WriteLine($"[DI Debug] Found DI method: {methodName} at {invocation.GetLocation().GetLineSpan().StartLinePosition}");

            // get type name and add it to the list
            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                {
                    var typeName = typeArg.ToString();
                    registeredServices.Add(typeName);
                    Console.WriteLine($"[DI Debug] Registered type from syntax: {typeName}");
                }
                return;
            }
        }

        private void CollectControllerConstructors(SyntaxNodeAnalysisContext context, AnalysisData analysisData)
        {
            var constructor = (ConstructorDeclarationSyntax)context.Node;

            // skip private constructors
            if (!constructor.Modifiers.Any(SyntaxKind.PublicKeyword))
                return;

            // get containing class
            var classDeclaration = constructor.Parent as ClassDeclarationSyntax;
            if (classDeclaration == null)
                return;

            // Check if it's a controller class
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null || !IsController(classSymbol))
                return;

            // Store the controller constructor info for later analysis
            analysisData.ControllerConstructors.Add(new ControllerConstructorInfo(
                constructor,
                classSymbol,
                context.SemanticModel));

            Console.WriteLine($"[DI Debug] Collected controller constructor: {classSymbol.Name}.{constructor.Identifier}");
        }

        private void AnalyzeControllerConstructor(
            CompilationAnalysisContext context,
            ControllerConstructorInfo controllerInfo,
            HashSet<string> registeredServices)
        {
            var constructor = controllerInfo.Constructor;
            var classSymbol = controllerInfo.ClassSymbol;

            Console.WriteLine($"\n[DI Debug] Analyzing controller constructor: {classSymbol.Name}.{constructor.Identifier}");

            // checking all constructor params
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                var parameterType = parameter.Type.ToString();
                Console.WriteLine($"[DI Debug] Checking parameter: {parameter.Identifier} of type {parameterType}");

                // check if type is not in the registered list
                if (!registeredServices.Contains(parameterType))
                {
                    Console.WriteLine($"[DI Debug] NOT REGISTERED: {parameterType}");
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        parameter.Type.GetLocation(),
                        parameterType);

                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    Console.WriteLine($"[DI Debug] Found registration for: {parameterType}");
                }
            }
        }

        private bool IsController(INamedTypeSymbol classSymbol)
        {
            // just checking if class name ends with "Controller"
            if (classSymbol.Name.EndsWith("Controller"))
            {
                Console.WriteLine($"[DI Debug] Class {classSymbol.Name} identified as controller by name convention");
                return true;
            }

            // check base types for ControllerBase
            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "ControllerBase" || baseType.Name == "Controller")
                {
                    Console.WriteLine($"[DI Debug] Class {classSymbol.Name} identified as controller by inheritance from {baseType.Name}");
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}