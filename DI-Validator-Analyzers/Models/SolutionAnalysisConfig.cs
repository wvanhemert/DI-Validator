using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    /// <summary>
    /// The configuration for the analysis.
    /// </summary>
    public class SolutionAnalysisConfig
    {
        /// <summary>
        /// Only set this when the analyzer cannot find the solution path automatically.
        /// Used to provide the solution path manually.
        /// </summary>
        public string? SolutionPath { get; set; }
        /// <summary>
        /// Provide the project path to identify the main entry project. Must be an ASP.NET Core web API with a WebApplicationBuilder.
        /// ProjectPath and ProjectType are interchangeable. Only use one of these at a time.
        /// </summary>
        public string? ProjectPath { get; set; }
        /// <summary>
        /// Provide a type within the main project to identify the main entry project. Must be an ASP.NET Core web API with a WebApplicationBuilder.
        /// ProjectPath and ProjectType are interchangeable. Only use one of these at a time.
        /// </summary>
        public Type? ProjectType { get; set; }
        /// <summary>
        /// A whitelist of severitíes shown in the output. This will override their effect on the test result as well.
        /// When left empty, defaults to allowing all severities.
        /// </summary>
        public IEnumerable<DiagnosticSeverity> SeverityFilter { get; set; } = Enumerable.Empty<DiagnosticSeverity>();
        public bool EnableLogging { get; set; } = false;
        public bool FailOnInfo { get; set; } = false;
    }
}
