using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RegistrationCollectionAnalyzer : DiagnosticAnalyzer
    {
        // -- Diagnostic info setup --
        public const string DiagnosticId = "DI000";
        private const string Category = "Dependency Injection";
        private static readonly LocalizableString Title = "Missing Dependency Injection Registration - Data collection";
        private static readonly LocalizableString MessageFormat = "";
        private static readonly LocalizableString Description = "";

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
        AnalysisData analysisData;

        public RegistrationCollectionAnalyzer(bool enableLogging, AnalysisData analysisData)
        {
            this.enableLogging = enableLogging;
            this.analysisData = analysisData;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            //context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (enableLogging)
                {
                    Log($"---- DI000 starting analysis of project: {compilationContext.Compilation.AssemblyName} ----");
                }

                // data only gathered from the main project, seen as the project that contains the entry point and builder targeted for this analysis
                if (compilationContext.Compilation.AssemblyName == analysisData.MainProjectAssemblyName)
                {
                    // Collect all DI registrations
                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => CollectRegisteredTypes(ctx),
                        SyntaxKind.InvocationExpression);

                    // Collect all extension methods called on WebApplicationBuilder.Services
                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => CollectCalledExtensionMethods(ctx),
                        SyntaxKind.InvocationExpression);
                }

                // data gathered from all projects for cross project analysis

                // Collect all defined classes, to find controllers and to parse dependencies needing injection inside DI registered services later
                context.RegisterSyntaxNodeAction(
                    ctx => CollectUserDefinedClass(ctx),
                    SyntaxKind.ClassDeclaration);


                // Collect all extension methods that register types with DI
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => CollectExtensionMethodRegistrations(ctx),
                    SyntaxKind.MethodDeclaration);
            });
        }

        private void CollectRegisteredTypes(SyntaxNodeAnalysisContext context)
        {
            var registeredServices = analysisData.RegisteredServices;
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
            Log($"Builder check: Checking '{expression}'");
            if (expression.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                Log($"Builder check: '{expression.Expression}' is not a member access expression.");
                return false;
            }

            if (memberAccess.Name.Identifier.Text != "Services")
            {
                Log($"Builder check: Member name is not 'Services' but '{memberAccess.Name.Identifier.Text}'.");
                return false;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (symbolInfo == null)
            {
                Log("Builder check: No symbol info for the expression before '.Services'.");
                return false;
            }

            ITypeSymbol? type = symbolInfo switch
            {
                ILocalSymbol local => local.Type,
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (type == null)
            {
                Log("Builder check: Could not determine type from symbol.");
                return false;
            }

            var isWebAppBuilder = type.Name == "WebApplicationBuilder" &&
                                  type.ContainingNamespace.ToDisplayString() == "Microsoft.AspNetCore.Builder";

            Log($"Builder check: Resolved type = {type}, isWebAppBuilder = {isWebAppBuilder}");

            return isWebAppBuilder;
        }

        private void CollectUserDefinedClass(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not ClassDeclarationSyntax classDecl)
                return;

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                return;

            // skip system/framework types or 3rd party libraries
            if (classSymbol.Locations.Any(loc => !loc.IsInSource))
                return;

            bool isController = IsController(classSymbol);

            // if the class has a primary constructor
            if (classDecl.ParameterList is not null)
            {
                analysisData.UserDefinedClasses.Add(new ClassInfo(
                    classDecl,
                    classSymbol,
                    context.SemanticModel,
                    isController));

                Log($"Collected user-defined primary constructor class: {classSymbol.Name}");
                return;
            }

            // otherwise look for public constructors
            foreach (var member in classDecl.Members.OfType<ConstructorDeclarationSyntax>())
            {
                if (!member.Modifiers.Any(SyntaxKind.PublicKeyword))
                    continue;

                analysisData.UserDefinedClasses.Add(new ClassInfo(
                    member,
                    classSymbol,
                    context.SemanticModel,
                    isController));

                Log($"Collected user-defined class with constructor: {classSymbol.Name}.{member.Identifier}");
            }
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


        private void CollectCalledExtensionMethods(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol is null || !methodSymbol.IsExtensionMethod)
                return;

            // If reduced, get original definition
            var originalMethodSymbol = methodSymbol.ReducedFrom ?? methodSymbol;

            if (originalMethodSymbol.ContainingNamespace.ToDisplayString().StartsWith(Helpers.MicrosoftAspNamespace) ||
                originalMethodSymbol.ContainingNamespace.ToDisplayString().StartsWith(Helpers.MicrosoftExtensionsNamespace))
                return;


            if (!IsFromWebApplicationBuilderServices(memberAccess, context.SemanticModel))
                return;

            var thisParam = originalMethodSymbol.Parameters.FirstOrDefault();
            if (thisParam is null || thisParam.Type.ToDisplayString() != Helpers.IServiceCollectionName)
                return;

            // Register the called extension method symbol using the original method
            analysisData.CalledExtensionMethods.Add(originalMethodSymbol.OriginalDefinition);
            Log($"Collected call to extension method: {originalMethodSymbol.Name}");

        }

        private void CollectExtensionMethodRegistrations(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not MethodDeclarationSyntax methodDecl)
                return;

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDecl);
            if (methodSymbol is null)
                return;

            // Only process methods extending IServiceCollection
            if (methodSymbol.Parameters.FirstOrDefault()?.Type.ToDisplayString() != Helpers.IServiceCollectionName)
                return;

            var body = methodDecl.Body;
            if (body == null) return;

            var registeredTypes = new List<ITypeSymbol>();
            List<IMethodSymbol> calledExtensionMethods = new();

            foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var invokedMethodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (invokedMethodSymbol == null)
                    continue;

                // Check for DI registration methods
                if (invokedMethodSymbol.ContainingNamespace.ToDisplayString() == Helpers.DiNamespace &&
                    Helpers.RegistrationMethods.Contains(invokedMethodSymbol.Name))
                {
                    if (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name is GenericNameSyntax generic &&
                        generic.TypeArgumentList.Arguments.FirstOrDefault() is TypeSyntax typeSyntax)
                    {
                        var typeSymbol = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
                        if (typeSymbol != null)
                        {
                            registeredTypes.Add(typeSymbol);
                            Log($"Extension method {methodSymbol.Name} registers {typeSymbol}");
                        }
                    }
                }
                else
                {
                    // Check for nested method call that has a parameter of IServiceCollection
                    // If reduced, get original definition
                    var originalMethodSymbol = invokedMethodSymbol.ReducedFrom ?? invokedMethodSymbol;
                    if (originalMethodSymbol.Parameters.FirstOrDefault()?.Type.ToDisplayString() == Helpers.IServiceCollectionName)
                    {
                        calledExtensionMethods.Add(originalMethodSymbol.OriginalDefinition);
                        Log($"Extension method {methodSymbol.Name} calls another extension method: {originalMethodSymbol.Name}");
                    }
                }
            }

            if (registeredTypes.Count > 0 || calledExtensionMethods.Count > 0)
                analysisData.ExtensionMethodRegistrations.Add(new ExtensionMethodData(methodSymbol.OriginalDefinition, registeredTypes, calledExtensionMethods));
        }




        private void Log(string message)
        {
            if (enableLogging) Console.WriteLine($"[DI Debug] {message}");
        }
    }
}
