using JrTools.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class ProcessGuardianService
    {
        /// <summary>
        /// Executa uma ação enquanto garante que uma lista de processos permaneça fechada.
        /// </summary>
        /// <param name="processos">Lista de nomes de processos a monitorar (ex: "BPrv230", "CS1")</param>
        /// <param name="acao">A tarefa principal a ser executada</param>
        /// <param name="progresso">Interface para reportar logs</param>
        public async Task ExecutarComProcessosFechadosAsync(string[] processos, Func<IProgress<string>, Task> acao, IProgress<string> progresso)
        {
            using var cts = new CancellationTokenSource();
            
            // Inicia a task de monitoramento em background
            var manterFechadoTask = MonitorarEMatarProcessosAsync(processos, progresso, cts.Token);

            try
            {
                // Executa a ação principal
                await acao(progresso);
            }
            finally
            {
                // Cancela o monitoramento ao final (sucesso ou erro)
                cts.Cancel();
                
                // Aguarda o encerramento gracioso do monitor
                try 
                { 
                    await manterFechadoTask; 
                } 
                catch (TaskCanceledException) { }
            }
        }

        private async Task MonitorarEMatarProcessosAsync(string[] processos, IProgress<string> progresso, CancellationToken token)
        {
            progresso?.Report("[GUARDIAN] Iniciando monitoramento de processos...");

            while (!token.IsCancellationRequested)
            {
                foreach (var nomeProcesso in processos)
                {
                    try
                    {
                        if (token.IsCancellationRequested) break;

                        int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso);

                        if (qtd > 0)
                        {
                            progresso?.Report($"[GUARDIAN] Detectado {nomeProcesso} ({qtd}). Eliminando...");
                            await ProcessKiller.KillByNameAsync(nomeProcesso, progresso);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Loga mas não interrompe o guardian
                        progresso?.Report($"[GUARDIAN WARN] Falha ao verificar/matar {nomeProcesso}: {ex.Message}");
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            progresso?.Report("[GUARDIAN] Monitoramento encerrado.");
        }
    }
}
