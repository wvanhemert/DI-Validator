using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    public class SolutionAnalysisConfig
    {
        public string? SolutionPath { get; set; }
        public string? ProjectPath { get; set; }
        public Type? ProjectType { get; set; }
        public IEnumerable<DiagnosticSeverity> SeverityFilter { get; set; } = Enumerable.Empty<DiagnosticSeverity>();
        public bool EnableLogging { get; set; } = false;
        public bool FailOnInfo { get; set; } = false;
    }
}
