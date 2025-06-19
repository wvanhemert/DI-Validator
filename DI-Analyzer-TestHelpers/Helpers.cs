using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DI_Analyzer_TestHelpers
{
    internal class Helpers
    {
        public static string? FindDirectoryWithFile(string startDir, string searchPattern)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (Directory.GetFiles(dir.FullName, searchPattern).Any())
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

    }
}
