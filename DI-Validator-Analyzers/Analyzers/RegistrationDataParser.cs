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

            

            foreach (var calledSymbol in analysisData.CalledExtensionMethods)
            {
                if (analysisData.ExtensionMethodRegistrations.TryGetExtensionRegistrationBySymbolOrName(calledSymbol, out var types))
                {
                    foreach (var type in types)
                    {
                        analysisData.RegisteredServices.Add(type);
                        if (enableLogging) Console.WriteLine($"[DI Debug] Resolved registration via {calledSymbol.Name}: {type}");
                    }
                }
                else
                {
                    if (enableLogging) Console.WriteLine($"[DI Debug] Method {calledSymbol.Name} was called by the builder but does not register any services.");
                }
            }

            // prepare unused services list
            analysisData.UnusedServices.UnionWith(analysisData.RegisteredServices);

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
    }
}
