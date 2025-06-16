using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Validator_Analyzers.Models
{
    internal class Helpers
    {
        // -- Constants --
        public static string DiNamespace = "Microsoft.Extensions.DependencyInjection";
        public static string ServiceCollectionServiceExtensions = "ServiceCollectionServiceExtensions";

        // The list of registration methods that we will recognize
        public static readonly HashSet<string> RegistrationMethods = new HashSet<string>
        {
            "AddSingleton", "AddScoped", "AddTransient",
            "TryAddSingleton", "TryAddScoped", "TryAddTransient"
        };

    }
}
