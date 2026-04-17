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
        /// Cria uma nova aplicação IIS vinculada a uma pool existente.
        /// Remove a aplicação existente se já houver uma com o mesmo nome/path.
        /// </summary>
        public Task CriarAplicacaoAsync(string site, string nomeApp, string pool, string caminhoFisico, IProgress<string> progresso)
            => Task.Run(() =>
            {
                progresso.Report($"[IIS] Site: {site} | App: /{nomeApp} | Pool: {pool}");
                progresso.Report($"[IIS] Caminho: {caminhoFisico}");

                using var mgr = new ServerManager();

                var siteObj = mgr.Sites[site]
                    ?? throw new InvalidOperationException($"Site '{site}' não encontrado no IIS.");

                var appPath = $"/{nomeApp}";

                // Remove se já existir
                var existente = siteObj.Applications.FirstOrDefault(a => a.Path == appPath);
                if (existente != null)
                {
                    progresso.Report("[IIS] Aplicação já existe, removendo...");
                    siteObj.Applications.Remove(existente);
                    mgr.CommitChanges();
                }

                var app = siteObj.Applications.Add(appPath, caminhoFisico);
                app.ApplicationPoolName = pool;
                mgr.CommitChanges();

                progresso.Report($"[IIS] Aplicação '/{nomeApp}' criada com sucesso.");
            });
    }
}
