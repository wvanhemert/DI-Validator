using DI_Validator_Analyzers.Models;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Analyzers
{
    public static class RegistrationDataParser
    {
        public static AnalysisData ParseExtensionMethodData(AnalysisData analysisData, bool enableLogging)
        {
            if (enableLogging) Console.WriteLine("[DI Debug] ----- Parsing extension method data -----");

            HashSet<IMethodSymbol> calledMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            foreach (var calledSymbol in analysisData.CalledExtensionMethods)
            {
                GetRegisteredTypesFromExtensionMethod(analysisData, calledSymbol, enableLogging);
            }

            // prepare unused services list
            var allSymbols = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var seenNames = new HashSet<string>();

            foreach (var symbol in analysisData.UnusedServices.Concat(analysisData.RegisteredServices))
            {
                var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (seenNames.Add(fqn))
                {
                    allSymbols.Add(symbol);
                }
            }

            analysisData.UnusedServices.UnionWith(allSymbols);



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
    }
}
