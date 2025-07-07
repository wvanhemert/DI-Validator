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
        public HashSet<ITypeSymbol> RegisteredServices { get; } = new HashSet<ITypeSymbol>(FQNSymbolComparer.Instance);
        public Dictionary<ITypeSymbol, ITypeSymbol> InterfaceImplementationDict { get; } = new(FQNSymbolComparer.Instance);
        public HashSet<ITypeSymbol> UnusedServices { get; } = new HashSet<ITypeSymbol>(FQNSymbolComparer.Instance);
        public List<ClassInfo> ControllerConstructors { get; set; } = new List<ClassInfo>();
        public List<ClassInfo> UserDefinedClasses { get; set; } = new();
        public HashSet<ITypeSymbol> RegisteredServiceDependencies { get; set; } = new(FQNSymbolComparer.Instance);

        public HashSet<IMethodSymbol> CalledExtensionMethods = new(FQNSymbolComparer.Instance);
        public HashSet<ITypeSymbol> VisitedClasses = new(FQNSymbolComparer.Instance);

        public List<ExtensionMethodData> ExtensionMethodRegistrations = new();
        public HashSet<IMethodSymbol> VisitedMethods = new(FQNSymbolComparer.Instance);
        
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
