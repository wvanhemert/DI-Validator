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
    // This analyzer is used to produce the unused dependency diagnostics.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnusedRegisteredDependencyAnalyzer : DiagnosticAnalyzer
    {
        // -- Diagnostic info setup --
        public const string DiagnosticId = "DI003";
        private const string Category = "Dependency Injection";
        private static readonly LocalizableString Title = "Unused DI Registration";
        private static readonly LocalizableString MessageFormat = "Type '{0}' is registered but not used by any constructor.";
        private static readonly LocalizableString Description = "This service was registered in the DI container but is never injected into any known constructor.";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        bool enableLogging = false;
        AnalysisData analysisData;

        public UnusedRegisteredDependencyAnalyzer(bool enableLogging, AnalysisData analysisData)
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
                    // Check if there are any unused registered services belonging to the current project
                    foreach (var unused in analysisData.UnusedServices
                        .Where(s => String.Equals(s.ContainingAssembly.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), ctx.Compilation.Assembly.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))
                    {
                        var location = unused.Locations.FirstOrDefault() ?? Location.None;

                        var diagnostic = Diagnostic.Create(
                            Rule,
                            location,
                            unused.ToDisplayString());

                        ctx.ReportDiagnostic(diagnostic);

                        Log($"Unused registered service found: {unused}");
                    }

                    Log($"--- Finished DI004 analysis of project: {ctx.Compilation.AssemblyName} ---");
                    Log("");
                });
            });
        }

        private void Log(string message)
        {
            if (enableLogging) Console.WriteLine($"[DI Debug] {message}");
        }
    }
}
