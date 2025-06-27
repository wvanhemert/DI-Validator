using DI_Validator_Analyzers.Analyzers;
using DI_Validator_Analyzers.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Concurrent;
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
            if (string.IsNullOrEmpty(config.ProjectPath))
                throw new ArgumentException("ProjectPath must be set in the configuration by either supplying a project path or a project type.");

            if (string.IsNullOrEmpty(config.SolutionPath))
                throw new ArgumentException("SolutionPath must be set in the configuration through a valid project path or project type.");

            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(config.SolutionPath);

            var diagnostics = new List<Diagnostic>();

            // determining main project's assembly name based on project path
            string? targetAssemblyName = null;
            var normalizedTargetPath = Path.GetFullPath(config.ProjectPath).Replace('\\', '/');

            foreach (var project in solution.Projects)
            {
                var projectFilePath = Path.GetFullPath(project.FilePath ?? "").Replace('\\', '/');
                if (string.Equals(projectFilePath, normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    targetAssemblyName = project.AssemblyName;
                    break;
                }
            }

            if (targetAssemblyName == null)
                throw new InvalidOperationException($"Could not find project with path: {config.ProjectPath}");

            AnalysisData data = new();

            data.MainProjectAssemblyName = targetAssemblyName;

            var collectionAnalyzers = new DiagnosticAnalyzer[]
            {
                new RegistrationCollectionAnalyzer(config.EnableLogging, data),
            };
            var validationAnalyzers = new DiagnosticAnalyzer[]
            {
                new RegistrationValidationAnalyzer(config.EnableLogging, data),
            };
            var unusedAnalyzers = new DiagnosticAnalyzer[]
            {
                new UnusedRegisteredDependencyAnalyzer(config.EnableLogging, data),
            };

            // running the collection
            if (config.EnableLogging) Console.WriteLine($"---- Starting collection of DI data for solution: {solution.FilePath} ----");

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var withAnalyzers = compilation.WithAnalyzers(collectionAnalyzers.ToImmutableArray());
                var results = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
            }

            data = RegistrationDataParser.ParseExtensionMethodData(data, config.EnableLogging);

            // running the validation
            if (config.EnableLogging) Console.WriteLine($"---- Starting validation for solution: {solution.FilePath} ----");

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var withAnalyzers = compilation.WithAnalyzers(validationAnalyzers.ToImmutableArray());
                var results = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

                foreach (var diag in results)
                {
                    if (config.SeverityFilter.Any() && !config.SeverityFilter.Contains(diag.Severity))
                        continue;

                    diagnostics.Add(diag);
                    // could put extra logging here
                }
            }

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var withAnalyzers = compilation.WithAnalyzers(unusedAnalyzers.ToImmutableArray());
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
