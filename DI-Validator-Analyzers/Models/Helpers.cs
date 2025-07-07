using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    internal static class Helpers
    {
        // -- Constants --
        public static string DiNamespace = "Microsoft.Extensions.DependencyInjection";
        public static string ServiceCollectionServiceExtensions = "ServiceCollectionServiceExtensions";
        public static string IServiceCollectionName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
        public static string MicrosoftAspNamespace = "Microsoft.AspNetCore";
        public static string MicrosoftExtensionsNamespace = "Microsoft.Extensions";

        // The list of registration methods that we will recognize
        public static readonly HashSet<string> RegistrationMethods = new HashSet<string>
        {
            "AddSingleton", "AddScoped", "AddTransient",
            "TryAddSingleton", "TryAddScoped", "TryAddTransient"
        };

        public static bool TryGetExtensionRegistrationBySymbolOrName(
            this List<ExtensionMethodData> extensionMethods,
            IMethodSymbol calledSymbol,
            out ExtensionMethodData extensionMethodData)
        {
            extensionMethodData = extensionMethods
                .FirstOrDefault(data => FQNSymbolComparer.Instance.Equals(data.MethodSymbol.OriginalDefinition, calledSymbol.OriginalDefinition)) ?? null!;
            return extensionMethodData != null;
        }

        public static bool TryGetMethodSymbol(this HashSet<IMethodSymbol> visitedMethods, IMethodSymbol calledSymbol, out IMethodSymbol methodSymbol)
        {
            methodSymbol = visitedMethods.FirstOrDefault(m => FQNSymbolComparer.Instance.Equals(m.OriginalDefinition, calledSymbol.OriginalDefinition)) ?? null!;
            return methodSymbol != null;
        }

        public static bool IsServiceRegistered(this HashSet<ITypeSymbol> registeredServices, ITypeSymbol? parameterSymbol)
        {
            if (parameterSymbol == null)
                return false;

            var paramDef = parameterSymbol.OriginalDefinition;

            if (registeredServices.Contains(paramDef))
                return true;

            return false;
        }

    }
}
