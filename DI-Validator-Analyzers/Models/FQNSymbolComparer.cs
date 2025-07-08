using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace DI_Validator_Analyzers.Models
{
    /// <summary>
    /// This symbol comparer falls back on using the fully qualified names of symbols to compare them when the regular equality check fails.
    /// This fallback is crucial for cross-compile equality, since the full equality changes per compilation.
    /// </summary>
    public class FQNSymbolComparer : IEqualityComparer<ISymbol>
    {
        public static readonly FQNSymbolComparer Instance = new();

        public bool Equals(ISymbol? x, ISymbol? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;

            return GetFqn(x) == GetFqn(y);
        }

        public int GetHashCode(ISymbol obj)
        {
            return GetFqn(obj).GetHashCode();
        }

        private static string GetFqn(ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }

}
