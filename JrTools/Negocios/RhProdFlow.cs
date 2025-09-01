using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services;
using System;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Negocios
{
    public class RhProdFlow
    {
        private readonly GitService _gitService;

        public RhProdFlow()
        {
            _gitService = new GitService();
        }

        public async Task<(bool Success, string Logs, string ErrorMessage)> ExecutarAsync(PageProdutoDataObject dto)
        {
            var logs = new StringBuilder();

            try
            {
                var branch = string.IsNullOrWhiteSpace(dto.BreachEspesificaDeTrabalho)
                    ? dto.Breach
                    : dto.BreachEspesificaDeTrabalho;

                logs.AppendLine($"[INFO] Iniciando fluxo com branch: {branch}");

                var checkoutHandler = new GitRhProdHandler(_gitService, branch);
                logs.AppendLine(await checkoutHandler.ExecutarCheckOutAsync());

                if (dto.AtualizarBreach)
                {
                    var pullHandler = new GitRhProdHandler(_gitService, branch);
                    logs.AppendLine(await pullHandler.ExecutarPullAsync());
                }


                return (true, logs.ToString(), string.Empty);
            }
            catch (Exception ex)
            {
                return (false, logs.ToString(), $"Erro durante execução: {ex.Message}");
            }
        }

    }
}
