using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DI_Validator_Analyzers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Workspaces;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using DI_Validator_Analyzers.Analyzers;

namespace DI_Validator_Tester
{
    // This is old code from a prototype. It's not used in the final solution.
    [TestClass]
    public class DependencyInjectionRegistrationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeWebApiProject_FindsMissingDependencies()
        {
            // Program.cs from test case
            var programCs = @"
using Services.Extensions;

/// FooApi 
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Onderstaande services zijn correct geregistreerd en worden gebruikt.
builder.Services.AddSingleton<ISingletonService, SingletonService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
builder.Services.AddTransient<ITransientService, TransientService>();

// Deze service is bewust uitgecommentarieerd om een foutmelding te genereren in MissingRegistrationController
//builder.Services.AddTransient<IUnregisteredService, UnregisteredService>();

// Onderstaande services hebben een registratie maar worden niet gebruikt in een controller en kunnen opgeruimd worden
builder.Services.AddTransient<ILegacyService, LegacyService>();

// Deze services hebben problemen in de dependencies van de services; uitgecomment omdat deze al ondervangen wordt op startup
//builder.Services.AddSingleton<IDependencyNoMatchingScopeService, DependencyNoMatchingScopeService>();

// Deze service wordt via een extension method toegevoegd (en is ongebruikt)
builder.Services.AddExtensionMethodService();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
";

            // Controller taken from test case, with an added registered service
            var controllerCs = @"
using Microsoft.AspNetCore.Mvc;

namespace FooApi.Controllers
{
    [ApiController]
    [Route(""[controller]"")]
    public class MissingRegistrationController : FooControllerBase
    {
        private readonly IUnregisteredService _unregisteredService;
        private readonly ISingletonService _singletonService;

        public MissingRegistrationController(IUnregisteredService unregisteredService, ISingletonService singletonService)
        {
            _unregisteredService = unregisteredService;
            _singletonService = singletonService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var guids = FormatGuids(_unregisteredService.GetOperationID());
            return Ok(guids);
        }
    }
}";

            // Run
            var diagnostics = GetDiagnostics(new[] { programCs, controllerCs });

            // Assert - only 1 should be found
            Assert.AreEqual(1, diagnostics.Length, "Expected exactly one diagnostic for the unregistered service");

            var diagnostic = diagnostics[0];
            Assert.AreEqual("DI001", diagnostic.Id);
            Assert.IsTrue(diagnostic.GetMessage().Contains("IUnregisteredService"));
            Assert.IsFalse(diagnostic.GetMessage().Contains("ISingletonService"));

            Console.WriteLine($"Found diagnostic: {diagnostic.GetMessage()} at {diagnostic.Location}");
        }

        [TestMethod]
        public void AnalyzeWebApiProject_FindsNoMissingDependencies()
        {
            // Program.cs from test case
            var programCs = @"
using Services.Extensions;

/// FooApi 
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Onderstaande services zijn correct geregistreerd en worden gebruikt.
builder.Services.AddSingleton<ISingletonService, SingletonService>();
builder.Services.AddScoped<IScopedService, ScopedService>();
builder.Services.AddTransient<ITransientService, TransientService>();

// Deze service is bewust uitgecommentarieerd om een foutmelding te genereren in MissingRegistrationController
//builder.Services.AddTransient<IUnregisteredService, UnregisteredService>();

// Onderstaande services hebben een registratie maar worden niet gebruikt in een controller en kunnen opgeruimd worden
builder.Services.AddTransient<ILegacyService, LegacyService>();

// Deze services hebben problemen in de dependencies van de services; uitgecomment omdat deze al ondervangen wordt op startup
//builder.Services.AddSingleton<IDependencyNoMatchingScopeService, DependencyNoMatchingScopeService>();

// Deze service wordt via een extension method toegevoegd (en is ongebruikt)
builder.Services.AddExtensionMethodService();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
";

            // Controller taken from test case, with an added registered service
            var controllerCs = @"
using Microsoft.AspNetCore.Mvc;

namespace FooApi.Controllers
{
    [ApiController]
    [Route(""[controller]"")]
    public class MissingRegistrationController : FooControllerBase
    {
        private readonly IUnregisteredService _unregisteredService;
        private readonly ISingletonService _singletonService;

        public MissingRegistrationController(ISingletonService singletonService)
        {

            _singletonService = singletonService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var guids = FormatGuids(_unregisteredService.GetOperationID());
            return Ok(guids);
        }
    }
}";

            // Run
            var diagnostics = GetDiagnostics(new[] { programCs, controllerCs });

            // Assert - 0 should be found
            Assert.AreEqual(0, diagnostics.Length, "Expected no unregistered service warnings");
        }


        private static Diagnostic[] GetDiagnostics(string[] sources)
        {
            var analyzer = new MissingRegistrationAnalyzer(true);

            var projectId = ProjectId.CreateNewId();
            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp);

            // add references
            solution = solution.AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            solution = solution.AddMetadataReference(
                projectId,
                MetadataReference.CreateFromFile(typeof(ControllerBase).Assembly.Location));

            var controllerBaseType = Type.GetType("Microsoft.AspNetCore.Mvc.ControllerBase, Microsoft.AspNetCore.Mvc.Core");
            if (controllerBaseType != null)
            {
                solution = solution.AddMetadataReference(
                    projectId,
                    MetadataReference.CreateFromFile(controllerBaseType.Assembly.Location));
            }

            var serviceCollectionType = Type.GetType("Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.Abstractions");
            if (serviceCollectionType != null)
            {
                solution = solution.AddMetadataReference(
                    projectId,
                    MetadataReference.CreateFromFile(serviceCollectionType.Assembly.Location));
            }

            // add documents
            int count = 0;
            foreach (var source in sources)
            {
                var documentId = DocumentId.CreateNewId(projectId);
                solution = solution.AddDocument(documentId, $"Test{count++}.cs", source);
            }

            // compilation
            var project = solution.GetProject(projectId);
            var compilation = project.GetCompilationAsync().Result;
            var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

            // run roslyn analyzer
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var diagnostics = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
            Console.WriteLine($"Analyzer completed in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Stop();
            return diagnostics.ToArray();
        }
    }
}
