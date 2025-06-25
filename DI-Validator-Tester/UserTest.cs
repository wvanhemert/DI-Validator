using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DI_Analyzer_TestHelpers;
using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;

namespace DI_Validator_Tester
{
    [TestClass]
    public class UserTest
    {
        [TestMethod]
        public async Task AnalyzeSolution()
        {
            var config = new SolutionAnalysisConfig
            {
                SolutionPath = @"",
                SeverityFilter = new[] { DiagnosticSeverity.Warning, DiagnosticSeverity.Error, DiagnosticSeverity.Info },
                EnableLogging = false,
                FailOnInfo = false,
            };
            await AnalyzerTestRunner.AssertSolution(config);

        }
    }
}
