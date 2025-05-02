using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DI_Validator_Analyzers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DI_Validator_Tester
{
    [TestClass]
    public class ProjectAnalyzerTests
    {
        [TestMethod]
        public async Task AnalyzeSourceFiles_WithoutMSBuild_FindsMissingDependencies()
        {
            // This test is validation for the ProjectAnalyzer class.
            // Create the analyzer instance
            var analyzer = new DependencyInjectionRegistrationAnalyzer();

            // Create temp directory for test files
            string tempDir = Path.Combine(Path.GetTempPath(), "DIAnalyzerTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a mock project file
                string projectPath = Path.Combine(tempDir, "TestProject.csproj");
                File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

                // Create a Program.cs with DI registration
                string programCs = @"
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<ISingletonService, SingletonService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
// The IUnregisteredService is missing on purpose

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.Run();
";
                File.WriteAllText(Path.Combine(tempDir, "Program.cs"), programCs);

                // Create a controller with a dependency
                string controllerCs = @"
using Microsoft.AspNetCore.Mvc;

namespace TestProject.Controllers
{
    [ApiController]
    [Route(""[controller]"")]
    public class TestController : ControllerBase
    {
        private readonly IUnregisteredService _unregisteredService;
        private readonly ISingletonService _singletonService;

        public TestController(IUnregisteredService unregisteredService, ISingletonService singletonService)
        {
            _unregisteredService = unregisteredService;
            _singletonService = singletonService;
        }

        [HttpGet]
        public IActionResult Get() => Ok(""Hello"");
    }
}";
                File.WriteAllText(Path.Combine(tempDir, "TestController.cs"), controllerCs);

                // Create interfaces
                string interfacesCs = @"
public interface ISingletonService {}
public interface IScopedService {}
public interface IUnregisteredService {}

public class SingletonService : ISingletonService {}
public class ScopedService : IScopedService {}
";
                File.WriteAllText(Path.Combine(tempDir, "Interfaces.cs"), interfacesCs);

                // Analyze the project
                var diagnostics = await ProjectAnalyzer.AnalyzeProjectAsync(projectPath, analyzer);

                // Filter for DI001 diagnostics
                var diIssues = diagnostics
                    .Where(d => d.Id == DependencyInjectionRegistrationAnalyzer.DiagnosticId)
                    .ToArray();

                // Verify results
                Assert.IsTrue(diIssues.Length > 0, "Expected at least one DI issue");
                Assert.IsTrue(
                    diIssues.Any(d => d.GetMessage().Contains("IUnregisteredService")),
                    "Expected to find issue with IUnregisteredService");

                // Print results
                foreach (var diagnostic in diIssues)
                {
                    Console.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()} at {diagnostic.Location.GetLineSpan()}");
                }
            }
            finally
            {
                // Clean up
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not clean up temp directory: {ex.Message}");
                }
            }
        }
        [TestMethod]
        public async Task AnalyzeRealProject_FindsMissingDependencies()
        {
            // Prototyping test project setting
            // Modify this path to point to your actual test project
            string projectPath = @"C:\Users\wvanhemert\source\repos\DIAssertionsCasus\FooApi\FooApi.csproj";

            if (!File.Exists(projectPath))
            {
                Console.WriteLine($"Test project not found at {projectPath}. Skipping test.");
                Assert.Inconclusive($"Test project not found at {projectPath}");
                return;
            }

            var analyzer = new DependencyInjectionRegistrationAnalyzer();

            // Analyze project
            var diagnostics = await ProjectAnalyzer.AnalyzeProjectAsync(projectPath, analyzer);

            // Print out all found diagnostics
            Console.WriteLine("\n----- DIAGNOSTIC RESULTS -----");
            foreach (var diagnostic in diagnostics)
            {
                Console.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()} at {diagnostic.Location.GetLineSpan()}");
            }
            Console.WriteLine("-----------------------------\n");
        }

        [TestMethod]
        public async Task AnalyzeMultipleProjects_FindsDIIssues()
        {
            // Prototyping test solution setting
            // Solution directory
            string solutionDir = @"C:\Users\wvanhemert\source\repos\DIAssertionsCasus";

            if (!Directory.Exists(solutionDir))
            {
                Assert.Inconclusive($"Solution directory not found at {solutionDir}");
                return;
            }

            // Find all project files
            var projectFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories);
            Console.WriteLine($"Found {projectFiles.Length} projects to analyze");

            var analyzer = new DependencyInjectionRegistrationAnalyzer();

            // Analyze each project
            foreach (var projectFile in projectFiles)
            {
                Console.WriteLine($"\nAnalyzing {Path.GetFileName(projectFile)}:");

                try
                {
                    var results = await ProjectAnalyzer.GetDiagnosticsOfTypeAsync(
                        projectFile,
                        analyzer,
                        DependencyInjectionRegistrationAnalyzer.DiagnosticId);

                    Console.WriteLine($"Found {results.Count} DI issues:");
                    foreach (var result in results)
                    {
                        Console.WriteLine($"  {result}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error analyzing project {projectFile}: {ex.Message}");
                }
            }
        }
    }
}