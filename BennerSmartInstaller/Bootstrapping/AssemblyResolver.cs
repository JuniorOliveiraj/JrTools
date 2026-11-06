using System;
using System.IO;
using System.Reflection;

namespace BennerSmartInstaller.Bootstrapping
{
    public static class AssemblyResolver
    {
        public static void Register(string binDir)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                try
                {
                    string asmName = new AssemblyName(resolveArgs.Name).Name + ".dll";
                    string asmPath = Path.Combine(binDir, asmName);
                    if (File.Exists(asmPath))
                    {
                        return Assembly.LoadFrom(asmPath);
                    }
                }
                catch
                {
                    // Fallback silencioso para outros resolvers
                }
                return null;
            };
        }
    }
}
