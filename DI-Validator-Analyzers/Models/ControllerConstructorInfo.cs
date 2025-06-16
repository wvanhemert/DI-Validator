using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    public class ControllerConstructorInfo
    {
        public ConstructorDeclarationSyntax Constructor { get; }
        public INamedTypeSymbol ClassSymbol { get; }
        public SemanticModel SemanticModel { get; }
        public string OriginAssemblyName { get; }

        public ControllerConstructorInfo(
            ConstructorDeclarationSyntax constructor,
            INamedTypeSymbol classSymbol,
            SemanticModel semanticModel)
        {
            Constructor = constructor;
            ClassSymbol = classSymbol;
            SemanticModel = semanticModel;
            OriginAssemblyName = semanticModel.Compilation.AssemblyName;
        }
    }
}
