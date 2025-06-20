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
                .FirstOrDefault(data => SymbolEqualityComparer.Default.Equals(data.MethodSymbol.OriginalDefinition, calledSymbol.OriginalDefinition)) ?? null!;
            if (extensionMethodData != null)
                return true;

            var calledDisplay = calledSymbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var method in extensionMethods)
            {
                if (method.MethodSymbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == calledDisplay)
                {
                    extensionMethodData = method;
                    return true;
                }
            }

            extensionMethodData = null!;
            return false;
        }

        public static bool TryGetMethodSymbol(this HashSet<IMethodSymbol> visitedMethods, IMethodSymbol calledSymbol, out IMethodSymbol methodSymbol)
        {
            methodSymbol = visitedMethods.FirstOrDefault(m => SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, calledSymbol.OriginalDefinition));
            if (methodSymbol != null)
                return true;
            var calledDisplay = calledSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            methodSymbol = visitedMethods.FirstOrDefault(m => m.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == calledDisplay);
            return methodSymbol != null;
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
