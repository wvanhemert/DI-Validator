using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    public class AnalysisData
    {
        public HashSet<ITypeSymbol> RegisteredServices { get; } = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        public HashSet<ITypeSymbol> UnusedServices { get; } = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        public List<ControllerConstructorInfo> ControllerConstructors { get; } = new List<ControllerConstructorInfo>();

        public HashSet<IMethodSymbol> CalledExtensionMethods = new(SymbolEqualityComparer.Default);

        public List<ExtensionMethodData> ExtensionMethodRegistrations = new();
        public HashSet<IMethodSymbol> VisitedMethods = new(SymbolEqualityComparer.Default);
        public string MainProjectAssemblyName { get; set; } = string.Empty;
    }

    public class ExtensionMethodData
    {
        public IMethodSymbol MethodSymbol { get; }
        public List<ITypeSymbol> RegisteredTypes { get; }
        public List<IMethodSymbol> CalledExtensionMethods { get; }
        public ExtensionMethodData(IMethodSymbol methodSymbol, List<ITypeSymbol> registeredTypes, List<IMethodSymbol> calledExtensionMethods)
        {
            MethodSymbol = methodSymbol;
            RegisteredTypes = registeredTypes;
            CalledExtensionMethods = calledExtensionMethods;
        }
    }
}
