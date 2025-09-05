using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services;
using System;
using System.Text;
using System.Threading;
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

        public async Task<(bool Success, string Logs, string ErrorMessage)> ExecutarAsync(
            PageProdutoDataObject dto,
            IProgress<string>? progresso = null)
        {
            var logs = new StringBuilder();

            void Log(string mensagem)
            {
                logs.AppendLine(mensagem);
                progresso?.Report(mensagem);
            }

            try
            {
                var branch = string.IsNullOrWhiteSpace(dto.BreachEspesificaDeTrabalho)
                    ? dto.Breach
                    : dto.BreachEspesificaDeTrabalho;

                Log($"[INFO] Iniciando fluxo com branch: {branch}");

                var config = await ConfigHelper.LerConfiguracoesAsync();

                var cts = new CancellationTokenSource();
                Task? guardianTask = null;




                var checkoutHandler = new GitRhProdHandler(_gitService, branch);
                await checkoutHandler.ExecutarCheckOutAsync(progresso, config.DiretorioProducao);

                if (dto.AtualizarBreach)
                {
                    var pullHandler = new GitRhProdHandler(_gitService, branch);
                    await pullHandler.ExecutarPullAsync(progresso, config.DiretorioProducao);
                }

                if (dto.RunnerFechado)
                    await ProcessKiller.KillCs1Async(progresso);

                if (dto.BuilderFechado)
                    await ProcessKiller.KillBuilderAsync(progresso);

                if (dto.PrividerFechado)
                {
                    void LogGuardian(string msg) => progresso?.Report($"[GUARDIAN] {msg}");
                    guardianTask = IniciarGuardianBPrv230Async(cts.Token, new Progress<string>(LogGuardian));
                }

                if (dto.AtualizarBinarios)
                {
                    var binarioFlow = new BinariosProdutoSrv(dto);
                    await binarioFlow.ExecutarBuscarBinarios(progresso, config.DiretorioBinarios);
                }
                if (dto.BuildarProjeto)
                {
                    string caminhoSln = @"D:\Benner\fontes\rh\prod\dotnet\Solutions\Compilacao_Completa_SemWebApp.sln";
                    var buildHandler = new BinldarProjetoSrv();
                    await buildHandler.BuildarProjetoAsync(caminhoSln, progresso);
                }

                Log("[INFO] Fluxo concluído com sucesso!");
                return (true, logs.ToString(), string.Empty);
            }
            catch (Exception ex)
            {
                Log($"[ERRO] Exceção durante execução: {ex.Message}");
                return (false, logs.ToString(), $"Erro durante execução: {ex.Message}");
            }
        }



        private async Task IniciarGuardianBPrv230Async(CancellationToken cancellationToken, IProgress<string>? progresso = null)
        {
            progresso?.Report("[INFO] Iniciando guardião BPrv230...");
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessKiller.KillBPrv230Async(progresso);
                try
                {
                    progresso?.Report("[INFO] Tentando matar provider...");
                    await Task.Delay(30000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    progresso?.Report("[ERRO] Erro ao matar BPrv230.");
                    break;
                }
            }
            progresso?.Report("[INFO] Guardião BPrv230 finalizado.");
        }
    }
}
