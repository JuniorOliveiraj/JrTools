using Benner.Tecnologia.Common.Application;
using System;

namespace BennerSmartInstaller
{
    /// <summary>
    /// Helper de inicialização do AppServer.
    /// Replica exatamente a lógica de WES.CLI.AppServerHelper.
    /// </summary>
    public static class AppServerHelper
    {
        public static void InitAppServer()
        {
            Console.WriteLine("[INFO] Inicializando o AppServer...");
            InitBefAnywhere();
            InitApplicationServer();
        }

        private static void InitApplicationServer()
        {
            // Força o carregamento da entidade de tabelas do BennerContext,
            // exatamente como o wes faz: BennerContext.TableSourceEntityService.ToString()
            try
            {
                Benner.Tecnologia.Common.BennerContext.TableSourceEntityService.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO] TableSourceEntityService: {ex.Message}");
            }
        }

        private static void InitBefAnywhere()
        {
            // WebBServerSystemService e WebLegacySystemService são classes 'internal' do assembly
            // Benner.Tecnologia.Bas.AppServer.BusinessLogic.dll, acessíveis ao wes.exe via
            // InternalsVisibleTo. Para o nosso executável, instanciamos via Activator.
            var appServerAsm = typeof(Benner.Tecnologia.Bas.AppServer.BusinessLogic.LegacyAppServer).Assembly;

            var bServerType = appServerAsm.GetType("Benner.Tecnologia.Bas.AppServer.BusinessLogic.WebBServerSystemService");
            var legacyType = appServerAsm.GetType("Benner.Tecnologia.Bas.AppServer.BusinessLogic.WebLegacySystemService");

            var bServer = (IBServerSystemService)Activator.CreateInstance(bServerType);
            var legacy = (ILegacySystemService)Activator.CreateInstance(legacyType);

            BennerAppInfraServices.InitDefault(bServer, legacy);
            BennerAppDbConfiguration.InitDefault(BennerAppInfraServices.Default);

            // Inicializa o DbContext padrão (BennerDbContextFactory)
            var dbFactoryType = appServerAsm.GetType("Benner.Tecnologia.Metadata.Entities.BennerDbContextFactory")
                             ?? typeof(BennerAppInfraServices).Assembly.GetType("Benner.Tecnologia.Common.Application.BennerDbContextFactory");

            if (dbFactoryType != null)
            {
                var factory = Activator.CreateInstance(dbFactoryType);
                var method = dbFactoryType.GetMethod("InitializeDefaultDbContext", Type.EmptyTypes);
                method?.Invoke(factory, null);
            }

            Console.WriteLine("[INFO] AppServer inicializado com sucesso.");
        }
    }
}
