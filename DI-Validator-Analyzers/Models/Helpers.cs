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
            this ConcurrentDictionary<IMethodSymbol, List<ITypeSymbol>> dict,
            IMethodSymbol calledSymbol,
            out List<ITypeSymbol> registrations)
        {
            if (dict.TryGetValue(calledSymbol.OriginalDefinition, out registrations))
                return true;

            var calledDisplay = calledSymbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var (key, value) in dict)
            {
                if (key.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == calledDisplay)
                {
                    registrations = value;
                    return true;
                }
            }

            registrations = null!;
            return false;
        }

        public static bool IsServiceRegistered(this HashSet<ITypeSymbol> registeredServices, ITypeSymbol? parameterSymbol)
        {
            if (parameterSymbol == null)
                return false;

            var paramDef = parameterSymbol.OriginalDefinition;

            // Direct match
            if (registeredServices.Contains(paramDef))
                return true;

            // Fallback to string-based comparison
            var paramName = paramDef.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return registeredServices.Any(registered =>
                registered.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == paramName);
        }

        public static bool TryRemoveFallback(this HashSet<ITypeSymbol> set, ITypeSymbol symbol)
        {
            if (set.Remove(symbol))
                return true;

            var targetName = symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (targetName == null) return false;

            var toRemove = set.FirstOrDefault(s =>
                s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetName);

            return toRemove != null && set.Remove(toRemove);
        }


    }
}
