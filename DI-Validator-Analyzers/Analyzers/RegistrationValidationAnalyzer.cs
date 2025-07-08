using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DI_Validator_Analyzers.Analyzers
{
    // this is the main diagnostic producing analyzer. It produces diagnostics for missing dependency warnings in controllers and registered services.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RegistrationValidationAnalyzer : DiagnosticAnalyzer
    {
        // -- Diagnostic info setup --
        public const string DiagnosticId = "DI001";
        private const string Category = "Dependency Injection";
        private static readonly LocalizableString Title = "Missing Dependency Injection Registration";
        private static readonly LocalizableString MessageFormat = "Type '{0}' is used in a Controller constructor but appears to be missing from DI registration.";
        private static readonly LocalizableString Description = "Type '{0}' used in a Controller constructor is not registered with dependency injection.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public const string ServiceDiagnosticId = "DI002";
        private const string ServiceCategory = "Dependency Injection";
        private static readonly LocalizableString ServiceTitle = "Missing Dependency Injection Registration";
        private static readonly LocalizableString ServiceMessageFormat = "Type '{0}' is used in a registered service but appears to be missing from DI registration.";
        private static readonly LocalizableString ServiceDescription = "Type '{0}' used in a registered service is not registered with dependency injection.";

        private static readonly DiagnosticDescriptor ServiceRule = new DiagnosticDescriptor(
            ServiceDiagnosticId,
            ServiceTitle,
            ServiceMessageFormat,
            ServiceCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: ServiceDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, ServiceRule);

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
                    Log($"---- DI001, DI002 starting validation of project: {compilationContext.Compilation.AssemblyName} ----");
                }

                compilationContext.RegisterCompilationEndAction(ctx =>
                {
                    // check if the types used in controller constructors are registered in the DI container
                    foreach (var controllerInfo in analysisData.ControllerConstructors)
                    {
                        AnalyzeControllerConstructor(ctx, controllerInfo);
                    }

                    if (ctx.Compilation.AssemblyName == analysisData.MainProjectAssemblyName)
                    {
                        foreach (var ServiceDependency in analysisData.RegisteredServicesDependencies)
                        {
                            AnalyzeServiceDependency(ctx, ServiceDependency);
                        }
                    }

                    Log($"--- Finished DI003 analysis of project: {ctx.Compilation.AssemblyName} ---");
                    Log("");
                });
            });
        }

        private void AnalyzeControllerConstructor(CompilationAnalysisContext context, ClassInfo controllerInfo)
        {
            var registeredServices = analysisData.RegisteredServices;
            var classSymbol = controllerInfo.ClassSymbol;

            if (controllerInfo.OriginAssemblyName != context.Compilation.AssemblyName)
                return;

            var parameters = ClassInfo.GetConstructorParameters(controllerInfo);

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
                    analysisData.UnusedServices.Remove(parameter.Type);
                }
            }

            Log("");
        }

        private void AnalyzeServiceDependency(CompilationAnalysisContext context, ITypeSymbol serviceDependency)
        {
            Log($"Analyzing Service dependency: {serviceDependency}");
            var registeredServices = analysisData.RegisteredServices;

            if (!registeredServices.Any(x => FQNSymbolComparer.Instance.Equals(x, serviceDependency)))
            {
                Log($"NOT REGISTERED: {serviceDependency}");
                var diagnostic = Diagnostic.Create(ServiceRule, null, serviceDependency);
                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                Log($"Found registration for: {serviceDependency}");
                analysisData.UnusedServices.Remove(serviceDependency);
            }
        }


        private void Log(string message)
        {
            if (enableLogging) Console.WriteLine($"[DI Debug] {message}");
        }
    }
}
