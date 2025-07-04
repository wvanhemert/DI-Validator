using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    public class ClassInfo
    {
        public ConstructorDeclarationSyntax? Constructor { get; }
        public ClassDeclarationSyntax? PrimaryConstructor { get; }
        public INamedTypeSymbol ClassSymbol { get; }
        public SemanticModel SemanticModel { get; }
        public string OriginAssemblyName { get; }

        public bool IsPrimaryConstructor => PrimaryConstructor != null;
        public bool IsControllerClass { get; }


        public ClassInfo(
            ConstructorDeclarationSyntax constructor,
            INamedTypeSymbol classSymbol,
            SemanticModel semanticModel,
            bool isController)
        {
            Constructor = constructor;
            ClassSymbol = classSymbol;
            SemanticModel = semanticModel;
            OriginAssemblyName = semanticModel.Compilation.AssemblyName!;
            IsControllerClass = isController;
        }

        public ClassInfo(
            ClassDeclarationSyntax primaryConstructor,
            INamedTypeSymbol classSymbol,
            SemanticModel semanticModel,
            bool isController)
        {
            Constructor = null;
            PrimaryConstructor = primaryConstructor;
            ClassSymbol = classSymbol;
            SemanticModel = semanticModel;
            OriginAssemblyName = semanticModel.Compilation.AssemblyName!;
            IsControllerClass = isController;
        }

        public static IEnumerable<IParameterSymbol> GetConstructorParameters(ClassInfo info)
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
