using System;
using System.IO;
using System.Reflection;
using BennerSmartInstaller.Bootstrapping;
using BennerSmartInstaller.Cli;
using BennerSmartInstaller.Services;

namespace BennerSmartInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  Benner Smart Installer - Instalação Seletiva");
            Console.WriteLine("==================================================");

            var cli = CommandLineParser.Parse(args);
            if (!cli.IsValid)
            {
                Console.WriteLine(cli.ErrorMessage);
                Environment.Exit(1);
                return;
            }

            string binDir = Path.Combine(cli.WebAppPath, "Bin");

            // 1. Inicializa Web.Config e Resolução de Assemblies do WebApp
            WebConfigLoader.Load(cli.WebAppPath);
            AssemblyResolver.Register(binDir);

            try
            {
                // 2. Inicializa o ambiente e containers WES
                BennerEnvironmentInitializer.Initialize(binDir);

                // 3. Executa o comando solicitado
                if (cli.CommandMode == "server")
                {
                    var server = new InteractiveServerService(cli.WebAppPath, binDir);
                    server.Run();
                }
                else if (cli.CommandMode == "compare")
                {
                    var comparator = new ArtifactComparatorService(cli.WebAppPath, binDir);
                    comparator.Execute();
                    Console.WriteLine("\n[SUCESSO] Comparação de artefatos concluída com êxito!");
                }
                else
                {
                    var installer = new ArtifactInstallerService(cli.WebAppPath, binDir);
                    installer.Execute(cli.FilesArg);
                    Console.WriteLine("\n[SUCESSO] Instalação concluída com êxito!");
                }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                var realEx = ex;
                while (realEx is TargetInvocationException && realEx.InnerException != null)
                {
                    realEx = realEx.InnerException;
                }
                Console.WriteLine($"\n[ERRO FATAL] Falha no instalador de artefatos: {realEx.Message}");
                Console.WriteLine($"[STACK TRACE]\n{realEx.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
