using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;

namespace DI_Validator_Tester
{
    /// <summary>
    /// A utility class that analyzes C# projects using source file analysis
    /// </summary>
    public class ProjectAnalyzer
    {
        /// <summary>
        /// Analyzes a project for dependency injection issues using source file analysis
        /// </summary>
        /// <param name="projectPath">Full path to the .csproj file</param>
        /// <param name="analyzer">The analyzer to run</param>
        /// <returns>Array of diagnostics found in the project</returns>
        public static async Task<Diagnostic[]> AnalyzeProjectAsync(string projectPath, DiagnosticAnalyzer analyzer)
        {
            // Validate project path
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException($"Project file not found at {projectPath}");
            }

            Console.WriteLine($"Loading project: {projectPath}");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Analyze source files directly
            return await AnalyzeSourceFilesAsync(projectPath, analyzer, stopwatch);
        }

        /// <summary>
        /// Analyzes C# source files in a project directory
        /// </summary>
        private static async Task<Diagnostic[]> AnalyzeSourceFilesAsync(string projectPath, DiagnosticAnalyzer analyzer, Stopwatch stopwatch)
        {
            // Get the directory containing the project
            var projectDir = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrEmpty(projectDir))
            {
                throw new ArgumentException("Invalid project path", nameof(projectPath));
            }

            // find all C# source files
            var sourceFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
            Console.WriteLine($"Found {sourceFiles.Length} source files to analyze");

            // create ad-hoc workspace
            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var project = workspace.AddProject(projectInfo);

            // add references to common assemblies
            var references = new[]
            {
                typeof(object).Assembly,                           // System.Private.CoreLib
                typeof(Console).Assembly,                          // System.Console
                typeof(Enumerable).Assembly,                       // System.Linq
                typeof(File).Assembly,                             // System.IO.FileSystem
                typeof(System.ComponentModel.DataAnnotations.KeyAttribute).Assembly, // System.ComponentModel.Annotations
                typeof(System.Net.Http.HttpClient).Assembly,       // System.Net.Http
                Assembly.Load("System.Runtime"),
                Assembly.Load("netstandard")
            };

            // try to add ASP.NET Core references if available
            try
            {
                var aspNetTypes = new[]
                {
                    "Microsoft.AspNetCore.Mvc.ControllerBase, Microsoft.AspNetCore.Mvc.Core",
                    "Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.Abstractions"
                };

                foreach (var typeName in aspNetTypes)
                {
                    try
                    {
                        var type = Type.GetType(typeName);
                        if (type != null)
                        {
                            references = references.Append(type.Assembly).ToArray();
                            Console.WriteLine($"Added reference to {type.Assembly.GetName().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not load ASP.NET Core reference: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading ASP.NET Core references: {ex.Message}");
            }

            foreach (var reference in references.Distinct())
            {
                try
                {
                    project = project.AddMetadataReference(
                        MetadataReference.CreateFromFile(reference.Location));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not add reference to {reference.GetName().Name}: {ex.Message}");
                }
            }

            // Add source documents
            // TODO: add source documents of dependency projects
            foreach (var sourceFile in sourceFiles)
            {
                try
                {
                    var sourceText = File.ReadAllText(sourceFile);
                    var fileName = Path.GetFileName(sourceFile);

                    var sourceTextObj = Microsoft.CodeAnalysis.Text.SourceText.From(sourceText);
                    workspace.AddDocument(projectId, fileName, sourceTextObj);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not add document {sourceFile}: {ex.Message}");
                }
            }

            // Get the updated project with documents
            project = workspace.CurrentSolution.GetProject(projectId);
            Console.WriteLine($"Added {project.Documents.Count()} documents to the ad-hoc project");

            // Create compilation
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                throw new InvalidOperationException("Failed to create compilation from source files");
            }

            Console.WriteLine($"Ad-hoc compilation created in {stopwatch.ElapsedMilliseconds}ms");

            // Add analyzers
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create(analyzer));

            // Run analysis
            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
            stopwatch.Stop();

            Console.WriteLine($"Analysis completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Found {diagnostics.Length} diagnostics");

            return diagnostics.ToArray();
        }

        /// <summary>
        /// Retrieves all the diagnostics of a specific type from a project
        /// </summary>
        /// <param name="projectPath">Path to the .csproj file</param>
        /// <param name="diagnosticId">The ID of the diagnostic to filter for (e.g., "DI001")</param>
        /// <returns>List of diagnostics with detailed information</returns>
        public static async Task<List<DiagnosticResult>> GetDiagnosticsOfTypeAsync(
            string projectPath,
            DiagnosticAnalyzer analyzer,
            string diagnosticId)
        {
            var results = new List<DiagnosticResult>();
            var diagnostics = await AnalyzeProjectAsync(projectPath, analyzer);

            foreach (var diagnostic in diagnostics.Where(d => d.Id == diagnosticId))
            {
                var result = new DiagnosticResult
                {
                    Id = diagnostic.Id,
                    Message = diagnostic.GetMessage(),
                    Severity = diagnostic.Severity,
                    Location = diagnostic.Location.GetLineSpan().ToString()
                };

                results.Add(result);
            }

            return results;
        }
    }

    /// <summary>
    /// Represents a diagnostic result with detailed information
    /// </summary>
    public class DiagnosticResult
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Location { get; set; }

        public override string ToString()
        {
            return $"[{Id}] {Message} at {Location}";
        }
    }
}