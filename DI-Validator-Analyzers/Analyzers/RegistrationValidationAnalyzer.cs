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
    public class RegistrationValidationAnalyzer : DiagnosticAnalyzer
    {
        // -- Diagnostic info setup --
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

        bool enableLogging = false;
        AnalysisData analysisData;

        public RegistrationValidationAnalyzer(bool enableLogging, AnalysisData analysisData)
        {
            this.enableLogging = enableLogging;
            this.analysisData = analysisData;
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                if (enableLogging)
                {
                    Log($"---- DI003 starting validation of project: {compilationContext.Compilation.AssemblyName} ----");
                }

                compilationContext.RegisterCompilationEndAction(ctx =>
                {
                    // check if the types used in controller constructors are registered in the DI container
                    foreach (var controllerInfo in analysisData.ControllerConstructors)
                    {
                        AnalyzeControllerConstructor(ctx, controllerInfo);
                    }

                    Log($"--- Finished DI003 analysis of project: {ctx.Compilation.AssemblyName} ---");
                    Log("");
                });
            });
        }

        private void AnalyzeControllerConstructor(CompilationAnalysisContext context, ControllerConstructorInfo controllerInfo)
        {
            var registeredServices = analysisData.RegisteredServices;
            var classSymbol = controllerInfo.ClassSymbol;

            if (controllerInfo.OriginAssemblyName != context.Compilation.AssemblyName)
                return;

            var parameters = ControllerConstructorInfo.GetConstructorParameters(controllerInfo);

            Log($"Analyzing controller constructor: {classSymbol.Name} ({(controllerInfo.IsPrimaryConstructor ? "primary" : "regular")})");

            foreach (var parameter in parameters)
            {
                Log($"Checking parameter: {parameter.Name} of type {parameter.Type}");

                if (!registeredServices.IsServiceRegistered(parameter.Type as INamedTypeSymbol))
                {
                    Log($"NOT REGISTERED: {parameter.Type}");

                    var syntaxRef = parameter.DeclaringSyntaxReferences.FirstOrDefault();
                    var syntax = syntaxRef?.GetSyntax() as ParameterSyntax;
                    var location = syntax?.Type?.GetLocation() ?? Location.None;

                    var diagnostic = Diagnostic.Create(Rule, location, parameter.Type);
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    Log($"Found registration for: {parameter.Type}");
                    analysisData.UnusedServices.TryRemoveFallback(parameter.Type as INamedTypeSymbol);
                }
            }

            Log("");
        }


        private void Log(string message)
        {
            if (enableLogging) Console.WriteLine($"[DI Debug] {message}");
        }
    }
}
