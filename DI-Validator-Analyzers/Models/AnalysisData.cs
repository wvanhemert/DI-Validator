using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    internal class AnalysisData
    {
        public HashSet<ITypeSymbol> RegisteredServices { get; } = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        public List<ControllerConstructorInfo> ControllerConstructors { get; } = new List<ControllerConstructorInfo>();
    }
}
