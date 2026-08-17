using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class IisService
    {
        /// <summary>
        /// Lista todos os Sites existentes no IIS local.
        /// </summary>
        public Task<List<string>> ListarSitesAsync()
            => Task.Run(() =>
            {
                using var mgr = new ServerManager();
                return mgr.Sites
                          .Select(s => s.Name)
                          .OrderBy(n => n)
                          .ToList();
            });

        /// <summary>
        /// Lista todos os Application Pools existentes no IIS local.
        /// </summary>
        public Task<List<string>> ListarPoolsAsync()
            => Task.Run(() =>
            {
                using var mgr = new ServerManager();
                return mgr.ApplicationPools
                          .Select(p => p.Name)
                          .OrderBy(n => n)
                          .ToList();
            });

        /// <summary>
        /// Cria uma nova aplicação IIS vinculada a uma pool (existente ou criada automaticamente).
        /// Se o site especificado não existir, ele é criado automaticamente no IIS.
        /// Remove a aplicação existente se já houver uma com o mesmo nome/path.
        /// </summary>
        public Task CriarAplicacaoAsync(string site, string nomeApp, string pool, string caminhoFisico, IProgress<string> progresso)
            => Task.Run(() =>
            {
                progresso.Report($"[IIS] Site: {site} | App: /{nomeApp} | Pool: {pool}");
                progresso.Report($"[IIS] Caminho: {caminhoFisico}");

                using var mgr = new ServerManager();

                // 1. Garantir que a Application Pool existe
                var poolObj = mgr.ApplicationPools.FirstOrDefault(p => p.Name.Equals(pool, StringComparison.OrdinalIgnoreCase));
                if (poolObj == null)
                {
                    progresso.Report($"[IIS] Application Pool '{pool}' não encontrada. Criando automaticamente...");
                    poolObj = mgr.ApplicationPools.Add(pool);
                    poolObj.ManagedRuntimeVersion = "v4.0";
                    poolObj.Enable32BitAppOnWin64 = true;
                    mgr.CommitChanges();
                    progresso.Report($"[IIS] Application Pool '{pool}' criada com sucesso.");
                }

                // 2. Garantir que o Site existe
                var siteObj = mgr.Sites.FirstOrDefault(s => s.Name.Equals(site, StringComparison.OrdinalIgnoreCase));
                if (siteObj == null)
                {
                    progresso.Report($"[IIS] Site '{site}' não encontrado no IIS. Criando automaticamente...");

                    // Encontra uma porta livre começando da 80 (ou 8080 caso a 80 esteja ocupada)
                    int porta = 80;
                    var portasEmUso = mgr.Sites
                        .SelectMany(s => s.Bindings)
                        .Select(b => b.EndPoint?.Port ?? 0)
                        .ToHashSet();

                    if (portasEmUso.Contains(porta))
                    {
                        porta = 8080;
                        while (portasEmUso.Contains(porta))
                        {
                            porta++;
                        }
                    }

                    siteObj = mgr.Sites.Add(site, "http", $"*:{porta}:", caminhoFisico);
                    siteObj.ApplicationDefaults.ApplicationPoolName = pool;
                    mgr.CommitChanges();
                    progresso.Report($"[IIS] Site '{site}' criado com sucesso na porta {porta}.");
                }

                var appPath = $"/{nomeApp.TrimStart('/')}";

                // 3. Remove aplicação se já existir no site
                var existente = siteObj.Applications.FirstOrDefault(a => a.Path.Equals(appPath, StringComparison.OrdinalIgnoreCase));
                if (existente != null)
                {
                    progresso.Report($"[IIS] Aplicação '{appPath}' já existe no site '{site}', removendo para recriar...");
                    siteObj.Applications.Remove(existente);
                    mgr.CommitChanges();
                }

                var app = siteObj.Applications.Add(appPath, caminhoFisico);
                app.ApplicationPoolName = pool;
                mgr.CommitChanges();

                progresso.Report($"[IIS] Aplicação '{appPath}' criada com sucesso no site '{site}'.");
            });
    }
}
