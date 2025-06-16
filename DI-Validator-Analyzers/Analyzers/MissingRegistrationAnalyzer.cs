using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DI_Validator_Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingRegistrationAnalyzer : DiagnosticAnalyzer
    {
        // -- Diagnostic info setup --
        public const string DiagnosticId = "DI001BETA";
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

        bool enableLogging = false;

        public MissingRegistrationAnalyzer(bool enableLogging)
        {
            this.enableLogging = enableLogging;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Use a two-phase approach to solve the race condition
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                Log($"--- Starting analysis of project: {compilationStartContext.Compilation.AssemblyName} ---");
                Log("");
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
                    if (enableLogging)
                    {
                        Log("");
                        Log("--------- REGISTERED TYPES ---------");
                        foreach (var type in analysisData.RegisteredServices.OrderBy(t => t.ToDisplayString()))
                        {
                            Log($"Registered: {type}");
                        }
                        Log("-----------------------------------");
                        Log("");
                    }

                    // Now analyze all collected controller constructors
                    foreach (var controllerInfo in analysisData.ControllerConstructors)
                    {
                        AnalyzeControllerConstructor(ctx, controllerInfo, analysisData.RegisteredServices);
                    }

                    Log($"--- Finished analysis of project: {ctx.Compilation.AssemblyName} ---");
                    Log("");
                });
            });
        }

        private void CollectRegisteredTypes(SyntaxNodeAnalysisContext context, HashSet<ITypeSymbol> registeredServices)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return;

            // Ensure it's from Microsoft.Extensions.DependencyInjection
            var containingNamespace = methodSymbol.ContainingNamespace.ToDisplayString();
            var containingType = methodSymbol.ContainingType?.Name;

            if (containingNamespace != Helpers.DiNamespace ||
                containingType != Helpers.ServiceCollectionServiceExtensions)
                return;

            var methodName = methodSymbol.Name;

            if (!Helpers.RegistrationMethods.Contains(methodName))
                return;

            // Walk up the expression to ensure it's rooted in something like builder.Services
            if (!IsFromWebApplicationBuilderServices(memberAccess, context.SemanticModel))
                return;

            Log($"Found DI method: {methodName} at {invocation.GetLocation().GetLineSpan().StartLinePosition}");

            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                var typeSymbol = context.SemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments.First()).Type;
                if (typeSymbol != null && registeredServices.Add(typeSymbol))
                {
                    Log($"Registered type: {typeSymbol}");
                }
            }
        }

        private bool IsFromWebApplicationBuilderServices(MemberAccessExpressionSyntax expression, SemanticModel semanticModel)
        {
            Log($"IsFromWebApplicationBuilderServices: Checking '{expression}'");
            if (expression.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                Log($"IsFromWebApplicationBuilderServices: '{expression.Expression}' is not a member access expression.");
                return false;
            }

            if (memberAccess.Name.Identifier.Text != "Services")
            {
                Log($"IsFromWebApplicationBuilderServices: Member name is not 'Services' but '{memberAccess.Name.Identifier.Text}'.");
                return false;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (symbolInfo == null)
            {
                Log("IsFromWebApplicationBuilderServices: No symbol info for the expression before '.Services'.");
                return false;
            }

            Log($"IsFromWebApplicationBuilderServices: Expression symbol kind = {symbolInfo.Kind}, name = {symbolInfo.Name}");

            ITypeSymbol? type = symbolInfo switch
            {
                ILocalSymbol local => local.Type,
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (type == null)
            {
                Log("IsFromWebApplicationBuilderServices: Could not determine type from symbol.");
                return false;
            }

            var isWebAppBuilder = type.Name == "WebApplicationBuilder" &&
                                  type.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Builder";

            Log($"IsFromWebApplicationBuilderServices: Resolved type = {type}, isWebAppBuilder = {isWebAppBuilder}");

            return isWebAppBuilder;
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

            Log($"Collected controller constructor: {classSymbol.Name}.{constructor.Identifier}");
        }

        private void AnalyzeControllerConstructor(
            CompilationAnalysisContext context,
            ControllerConstructorInfo controllerInfo,
            HashSet<ITypeSymbol> registeredServices)
        {
            var constructor = controllerInfo.Constructor;
            var classSymbol = controllerInfo.ClassSymbol;

            Log($"Analyzing controller constructor: {classSymbol.Name}.{constructor.Identifier}");

            // checking all constructor params
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                var parameterSymbol = controllerInfo.SemanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
                Log($"Checking parameter: {parameter.Identifier} of type {parameterSymbol}");

                // check if type is not in the registered list
                if (!registeredServices.Contains(parameterSymbol))
                {
                    Log($"NOT REGISTERED: {parameterSymbol}");
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        parameter.Type.GetLocation(),
                        parameterSymbol);

                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    Log($"Found registration for: {parameterSymbol}");
                }
            }
            Log("");
        }

        private bool IsController(INamedTypeSymbol classSymbol)
        {
            // just checking if class name ends with "Controller"
            if (classSymbol.Name.EndsWith("Controller"))
            {
                Log($"Class {classSymbol.Name} identified as controller by name convention");
                return true;
            }

            // check base types for ControllerBase
            var baseType = classSymbol.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "ControllerBase" || baseType.Name == "Controller")
                {
                    Log($"Class {classSymbol.Name} identified as controller by inheritance from {baseType.Name}");
                    return true;
                }

                baseType = baseType.BaseType;
            }

            return false;
        }

        private void Log(string message)
        {
            if (enableLogging) Console.WriteLine($"[DI Debug] {message}");
        }

    }
}