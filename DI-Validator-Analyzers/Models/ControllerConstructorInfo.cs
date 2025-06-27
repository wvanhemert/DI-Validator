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
        public ConstructorDeclarationSyntax? Constructor { get; }
        public ClassDeclarationSyntax? PrimaryConstructor { get; }
        public INamedTypeSymbol ClassSymbol { get; }
        public SemanticModel SemanticModel { get; }
        public string OriginAssemblyName { get; }

        public bool IsPrimaryConstructor => PrimaryConstructor != null;


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

        public ControllerConstructorInfo(
        ClassDeclarationSyntax primaryConstructor,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
        {
            Constructor = null;
            PrimaryConstructor = primaryConstructor;
            ClassSymbol = classSymbol;
            SemanticModel = semanticModel;
            OriginAssemblyName = semanticModel.Compilation.AssemblyName!;
        }

        public static IEnumerable<IParameterSymbol> GetConstructorParameters(ControllerConstructorInfo info)
        {
            if (info.Constructor != null)
            {
                var ctorSymbol = info.SemanticModel.GetDeclaredSymbol(info.Constructor) as IMethodSymbol;
                return ctorSymbol?.Parameters ?? Enumerable.Empty<IParameterSymbol>();
            }
            else if (info.PrimaryConstructor != null)
            {
                var ctorSymbol = info.ClassSymbol
                    .InstanceConstructors
                    .FirstOrDefault(c => !c.IsStatic && c.Parameters.Length > 0);

                return ctorSymbol?.Parameters ?? Enumerable.Empty<IParameterSymbol>();
            }

            return Enumerable.Empty<IParameterSymbol>();
        }

    }
}
