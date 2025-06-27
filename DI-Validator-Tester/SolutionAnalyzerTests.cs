using DI_Analyzer_TestHelpers;
using DI_Validator_Analyzers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Tester
{
    [TestClass]
    public class SolutionAnalyzerTests
    {
        [TestMethod]
        public async Task AnalyzeSolution()
        {
            // Arrange
            var solutionPath = @"C:\Users\wvanhemert\source\repos\DIAssertionsCasus\DIAssertionsCasus.sln";
            var projectPath = @"C:\Users\wvanhemert\source\repos\DIAssertionsCasus\FooApi\FooApi.csproj";
            var config = new DI_Validator_Analyzers.Models.SolutionAnalysisConfig
            {
                SolutionPath = solutionPath,
                ProjectPath = projectPath,
                SeverityFilter = new[] { DiagnosticSeverity.Warning, DiagnosticSeverity.Error, DiagnosticSeverity.Info },
                EnableLogging = true,
                FailOnInfo = false,
            };

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
                Console.WriteLine($"{diag.Id} ({diag.Severity}): {diag.GetMessage()}");
            }
            Console.WriteLine($"-------------------------------------------");

            // Assert
            Assert.IsFalse(diagnostics.Any(d => d.Id == "DI001"), "Some expected dependencies are not registered.");
            Assert.IsFalse(diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Warning), "Dependency Injection Warnings were found.");
            if (config.FailOnInfo)
            {
                Assert.IsFalse(diagnostics.Any(d => d.Severity >= DiagnosticSeverity.Info), "Dependency Injection info was produced, and counted as a warning.");
                Assert.IsFalse(diagnostics.Any());
            }
            else Assert.IsTrue(diagnostics.Any(d => d.Severity <= DiagnosticSeverity.Info || !diagnostics.Any()));


        }

        [TestMethod]
        public async Task AnalyzeSolutionWithHelper()
        {
            // Arrange
            var config = new DI_Validator_Analyzers.Models.SolutionAnalysisConfig
            {
                ProjectType = typeof(SolutionAnalyzerTests),
                SeverityFilter = new[] { DiagnosticSeverity.Warning, DiagnosticSeverity.Error, DiagnosticSeverity.Info },
                EnableLogging = false,
                FailOnInfo = false,
            };
            await AnalyzerTestRunner.AssertSolution(config);
        }
    }
}
