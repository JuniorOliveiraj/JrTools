using Benner.Tecnologia.Bas.AppServer.BusinessLogic;
using Benner.Tecnologia.Common;
using Benner.Tecnologia.Common.IoC;
using Benner.Tecnologia.Wes.Components.WebApp.IoC;
using Benner.Tecnologia.Wes.IoC;
using BennerSmartInstaller.Verbs;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BennerSmartInstaller
{
    class Program
    {
        private static int returnValue;

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("[BENNER SMART INSTALLER] Utilitário de Instalação Seletiva Nativa Benner WES");

            Initialize();

            try
            {
                Parser.Default.ParseArguments<SmartInstallVerb>(args)
                    .WithParsed(ExecuteCommand)
                    .WithNotParsed(ParseError);
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("Comando não encontrado.");
                returnValue = 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Parser exception: " + ex.Message);
                returnValue = 1;
            }
            finally
            {
                LegacyAppServer.Stop();
            }

            return returnValue;
        }

        private static void ExecuteCommand(object arg)
        {
            BaseVerb baseVerb = null;
            try
            {
                baseVerb = (BaseVerb)arg;
                baseVerb.Execute();
            }
            catch (Exception ex)
            {
                bool verbose = baseVerb != null && baseVerb.Verbose;
                PrintException(ex, verbose);
                returnValue = 1;
            }
        }

        private static void ParseError(IEnumerable<Error> errors)
        {
            returnValue = 1;
        }

        public static void PrintException(Exception ex, bool verbose = false)
        {
            Console.WriteLine(ex.Message);
            if (!verbose) return;
            Console.WriteLine(Environment.NewLine + "=== Exception ===");
            Console.WriteLine(ex.ToString());
        }

        /// <summary>
        /// Inicialização idêntica ao wes: Program.Initialize() + AppServerHelper.InitAppServer()
        /// </summary>
        private static void Initialize()
        {
            // 1. IoC - Exatamente como wes.Program.Initialize()
            DependencyContainer.Start(new WesCompositionRoot());
            WesServicesRegisterer.RegisterInternalBEFServices(DependencyContainer.InternalKernel);
            WesServicesRegisterer.RegisterPublicBEFServices(DependencyContainer.InternalKernel);
            WesServicesRegisterer.RegisterTemporaryFileStorageModule(DependencyContainer.InternalKernel);
            HelpersModuleStatic.RegisterModule(DependencyContainer.InternalKernel);

            // 2. APPBASE - Exatamente como wes.Program.Initialize()
            string wesFolder = BennerConfiguration.WesFolder;
            if (AppDomain.CurrentDomain.SetupInformation.ApplicationName
                .Equals("BennerSmartInstaller.exe", StringComparison.OrdinalIgnoreCase)
                && wesFolder.EndsWith("\\bin\\", StringComparison.InvariantCultureIgnoreCase))
            {
                wesFolder = Directory.GetParent(wesFolder.TrimEnd('\\', '/')).FullName;
            }
            AppDomain.CurrentDomain.SetData("APPBASE", wesFolder);

            // 3. WebConfig - Exatamente como wes.CLIConfiguration.LoadWebConfig()
            CLIConfiguration.LoadWebConfig();

            // 4. UnblockPool - Exatamente como wes.Program.Initialize()
            LegacyAppServer.UnblockPool();

            // 5. AppServer - Exatamente como wes.AppServerHelper.InitAppServer()
            AppServerHelper.InitAppServer();
        }
    }
}
