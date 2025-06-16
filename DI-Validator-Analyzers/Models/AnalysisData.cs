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

        public ConcurrentDictionary<IMethodSymbol, List<ITypeSymbol>> ExtensionMethodRegistrations = new(SymbolEqualityComparer.Default);
    }
}
