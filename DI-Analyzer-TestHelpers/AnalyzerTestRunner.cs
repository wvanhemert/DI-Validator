using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DI_Validator_Analyzers;
using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DI_Analyzer_TestHelpers
{
    public static class AnalyzerTestRunner
    {
        /// <summary>
        /// The entry point for running the Dependency Injection Analyzer against a solution. Use this within an MSTest test method to validate the solution's dependency injection setup.
        /// </summary>
        /// <param name="config">The configuration to be used with this analysis run.</param>
        /// <returns>MSTest result based on analysis</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public static async Task AssertSolution(SolutionAnalysisConfig config)
        {
            // Arrange
            if (string.IsNullOrEmpty(config.ProjectPath))
            {
                // try to set the path based on the project type
                if (config.ProjectType == null)
                    throw new ArgumentException("Either ProjectPath or ProjectType must be set.");

                var assemblyPath = config.ProjectType.Assembly.Location;
                var dir = Path.GetDirectoryName(assemblyPath)!;

                // seek up to .csproj file
                var projectDir = Helpers.FindDirectoryWithFile(dir, "*.csproj");
                if (projectDir == null)
                    throw new FileNotFoundException("Could not find a .csproj file upward from " + dir);
                config.ProjectPath = Directory.GetFiles(projectDir, "*.csproj").First();

                // seek up to .sln
                var solutionDir = Helpers.FindDirectoryWithFile(projectDir, "*.sln");
                if (solutionDir == null)
                    throw new FileNotFoundException("Could not find a .sln file upward from project directory.");

                var slnPath = Directory.GetFiles(solutionDir, "*.sln").First();
                config.SolutionPath = slnPath;
            }
            else
            {
                // seek up to .sln
                var directory = Path.GetDirectoryName(config.ProjectPath);
                if (directory == null)
                    throw new ArgumentException("ProjectPath must be a valid file path.");

                var solutionDir = Helpers.FindDirectoryWithFile(directory, "*.sln");
                if (solutionDir == null)
                    throw new FileNotFoundException("Could not find a .sln file upward from project directory.");

                var slnPath = Directory.GetFiles(solutionDir, "*.sln").First();
                config.SolutionPath = slnPath;
            }

            List<Diagnostic> diagnostics = new List<Diagnostic>();

            // Act
            try
            {
                diagnostics = await DependencyInjectionAnalyzer.AnalyzeSolutionAsync(config);
            }
            catch (FileNotFoundException)
            {
                Assert.Inconclusive("Solution not found. Ensure the solution path is correct.");
            }

            Console.WriteLine($"---------- RESULT: {diagnostics.Count} diagnostics ----------");



            foreach (var diag in diagnostics)
            {
                if (diag.Id == "DI001")
                {
                    Console.WriteLine($"{diag.Id} ({diag.Severity}): {diag.GetMessage()} - {GetFromLocation(diag.Location)}");
                } else if (diag.Id == "DI002" || diag.Id == "DI003")
                {
                    Console.WriteLine($"{diag.Id} ({diag.Severity}): {diag.GetMessage()}");
                }
                    
            }
            Console.WriteLine($"-------------------------------------------");

            // Assert
            Assert.IsFalse(diagnostics.Any(d => d.Id == "DI001" || d.Id == "DI002"), "Some expected dependencies are not registered.");
            Assert.IsFalse(diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Warning), "Dependency Injection Warnings were found.");
            if (config.FailOnInfo)
            {
                Assert.IsFalse(diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Info), "Dependency Injection info was produced, and counted as a warning.");
                Assert.IsFalse(diagnostics.Any());
            }
            else Assert.IsTrue(diagnostics.Any(d => d.Severity <= DiagnosticSeverity.Info) || !diagnostics.Any());


        }

        static string GetFromLocation(Location location)
        {
            if (location == null || !location.IsInSource)
                return null;

            var sourceTree = location.SourceTree;
            var span = location.SourceSpan;

            // Full file path
            var filePath = sourceTree?.FilePath ?? "<unknown>";

            // Get line and column info from the syntax tree's line mapping
            var lineSpan = sourceTree.GetLineSpan(span);

            // Roslyn lines and columns are zero-based, VS expects 1-based
            int line = lineSpan.StartLinePosition.Line + 1;
            int column = lineSpan.StartLinePosition.Character + 1;

            return $"{filePath}({line},{column})";
        }
    }
}
