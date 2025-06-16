using DI_Validator_Analyzers.Analyzers;
using DI_Validator_Analyzers.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers
{
    public static class DependencyInjectionAnalyzer
    {
        public static async Task<List<Diagnostic>> AnalyzeSolutionAsync(SolutionAnalysisConfig config)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(config.SolutionPath);

            var diagnostics = new List<Diagnostic>();
            var analyzers = new DiagnosticAnalyzer[]
            {
                new MissingRegistrationAnalyzer(config.EnableLogging),
            };

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray());
                var results = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

                foreach (var diag in results)
                {
                    if (config.SeverityFilter.Any() && !config.SeverityFilter.Contains(diag.Severity))
                        continue;

                    diagnostics.Add(diag);
                    // could put extra logging here
                }
            }

            return diagnostics;
        }
    }
}
