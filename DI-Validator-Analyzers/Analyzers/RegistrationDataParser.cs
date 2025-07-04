using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Analyzers
{
    public static class RegistrationDataParser
    {
        public static AnalysisData ParseData(AnalysisData analysisData, bool enableLogging)
        {
            analysisData = ParseExtensionMethodData(analysisData, enableLogging);

            analysisData = ParseServicesDependencyData(analysisData, enableLogging);

            // prepare controller constructor list
            analysisData.ControllerConstructors = analysisData.UserDefinedClasses.Where(x => x.IsControllerClass && x.OriginAssemblyName == analysisData.MainProjectAssemblyName).ToList();

            // prepare unused services list
            analysisData.UnusedServices.UnionWith(analysisData.RegisteredServices);
            analysisData.UnusedServices.RemoveDuplicatesByName();

            // Debug output of registered types
            if (enableLogging)
            {
                Console.WriteLine("[DI Debug]");
                Console.WriteLine("[DI Debug] --------- REGISTERED TYPES ---------");
                foreach (var type in analysisData.RegisteredServices.OrderBy(t => t.ToDisplayString()))
                {
                    Console.WriteLine($"[DI Debug] Registered: {type}");
                }
                Console.WriteLine("[DI Debug] -----------------------------------");
                Console.WriteLine("[DI Debug]");
            }
            return analysisData;
        }
        public static AnalysisData ParseExtensionMethodData(AnalysisData analysisData, bool enableLogging)
        {
            if (enableLogging) Console.WriteLine("[DI Debug] ----- Parsing extension method data -----");

            foreach (var calledSymbol in analysisData.CalledExtensionMethods)
            {
                GetRegisteredTypesFromExtensionMethod(analysisData, calledSymbol, enableLogging);
            }

            return analysisData;
        }

        private static void GetRegisteredTypesFromExtensionMethod(AnalysisData analysisData, IMethodSymbol calledSymbol, bool enableLogging)
        {
            if (analysisData.ExtensionMethodRegistrations.TryGetExtensionRegistrationBySymbolOrName(calledSymbol, out var extensionMethodData))
            {
                if (analysisData.VisitedMethods.TryGetMethodSymbol(calledSymbol, out var methodSymbol))
                {
                    if (enableLogging) Console.WriteLine($"[DI Debug] Method {methodSymbol.Name} was already visited, skipping further analysis.");
                    return;
                }
                analysisData.VisitedMethods.Add(extensionMethodData.MethodSymbol);
                foreach (var type in extensionMethodData.RegisteredTypes)
                {
                    analysisData.RegisteredServices.Add(type);
                    if (enableLogging) Console.WriteLine($"[DI Debug] Resolved registration via {calledSymbol.Name}: {type}");
                }
                foreach (var extMethod in extensionMethodData.CalledExtensionMethods)
                {
                    if (enableLogging) Console.WriteLine($"[DI Debug] Analyzing extension method {extMethod.Name} recursively for further registrations in this path.");
                    GetRegisteredTypesFromExtensionMethod(analysisData, extMethod, enableLogging);
                    
                }
            }
            else
            {
                if (enableLogging) Console.WriteLine($"[DI Debug] Method {calledSymbol.Name} was called by the builder but does not register any services.");
            }
        }

        public static AnalysisData ParseServicesDependencyData(AnalysisData analysisData, bool enableLogging)
        {
            if (enableLogging) Console.WriteLine("[DI Debug] ----- Parsing service dependencies data -----");

            HashSet<ITypeSymbol> foundDependencies = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var registeredDependency in analysisData.RegisteredServices)
            {
                var dependencies = GetDependenciesFromClass(analysisData, registeredDependency);

                HashSet<ITypeSymbol> typeSymbols = new HashSet<ITypeSymbol>(dependencies, SymbolEqualityComparer.Default);

                analysisData.RegisteredServiceDependencies.UnionWith(typeSymbols);
            }

            analysisData.RegisteredServiceDependencies.RemoveDuplicatesByName();

            return analysisData;
        }

        public static IEnumerable<ITypeSymbol> GetDependenciesFromClass(AnalysisData analysisData, ITypeSymbol root)
        {
            var queue = new Queue<ITypeSymbol>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (!analysisData.VisitedClasses.Add(current))
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(current, root))
                    yield return current;

                var classInfo = analysisData.UserDefinedClasses
                    .FirstOrDefault(c => c.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                if (classInfo == null)
                    continue;

                foreach (var param in ClassInfo.GetConstructorParameters(classInfo))
                {
                    if (param.Type is ITypeSymbol paramType)
                        queue.Enqueue(paramType);
                }
            }
        }

        private static HashSet<ITypeSymbol> RemoveDuplicatesByName(this HashSet<ITypeSymbol> list)
        {
            var allSymbols = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var seenNames = new HashSet<string>();

            foreach (var symbol in list)
            {
                var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (seenNames.Add(fqn))
                {
                    allSymbols.Add(symbol);
                }
            }

            return allSymbols;
        }
    }
}
