using System;
using System.IO;

namespace BennerSmartInstaller.Cli
{
    public static class CommandLineParser
    {
        public static CommandLineArgs Parse(string[] args)
        {
            var parsed = new CommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("compare", StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals("--compare", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.CommandMode = "compare";
                }
                else if (args[i].Equals("install", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.CommandMode = "install";
                }
                else if (args[i].Equals("server", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("daemon", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("interactive", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                         args[i].Equals("--server", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.CommandMode = "server";
                }
                else if (args[i] == "-a" && i + 1 < args.Length)
                {
                    parsed.WebAppPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-f" && i + 1 < args.Length)
                {
                    parsed.FilesArg = args[i + 1];
                    i++;
                }
            }

            if (string.IsNullOrWhiteSpace(parsed.WebAppPath))
            {
                parsed.IsValid = false;
                parsed.ErrorMessage = "Uso: BennerSmartInstaller.exe install -a \"<caminho_webApp>\" -f \"<arquivo1;arquivo2>\"\n" +
                                      "Ou:  BennerSmartInstaller.exe compare -a \"<caminho_webApp>\"\n" +
                                      "Ou:  BennerSmartInstaller.exe server -a \"<caminho_webApp>\"";
                return parsed;
            }

            if (parsed.CommandMode == "install" && string.IsNullOrWhiteSpace(parsed.FilesArg))
            {
                parsed.IsValid = false;
                parsed.ErrorMessage = "Uso em modo instalação: BennerSmartInstaller.exe install -a \"<caminho_webApp>\" -f \"<arquivo1;arquivo2>\"";
                return parsed;
            }

            string binDir = Path.Combine(parsed.WebAppPath, "Bin");
            if (!Directory.Exists(binDir))
            {
                parsed.IsValid = false;
                parsed.ErrorMessage = $"[ERRO] Pasta Bin do WebApp não encontrada em: {binDir}";
                return parsed;
            }

            parsed.IsValid = true;
            return parsed;
        }
    }
}
