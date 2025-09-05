using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public static class ProcessKiller
    {
        public static Task KillBPrv230Async(IProgress<string>? progresso = null)
            => KillByNameAsync("BPrv230", progresso);

        public static Task KillCs1Async(IProgress<string>? progresso = null)
            => KillByNameAsync("cs1", progresso);

        public static Task KillBuilderAsync(IProgress<string>? progresso = null)
            => KillByNameAsync("builder", progresso);

        private static Task KillByNameAsync(string processName, IProgress<string>? progresso = null)
        {
            return Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        proc.Kill(true);
                        progresso?.Report($"[INFO] Processo {proc.ProcessName} [{proc.Id}] encerrado.");
                        Console.WriteLine($"Processo {proc.ProcessName} [{proc.Id}] encerrado.");
                    }
                    catch (Exception ex)
                    {
                        progresso?.Report($"[ERRO] Falha ao matar {processName}: {ex.Message}");
                        Console.WriteLine($"Erro ao matar {processName}: {ex.Message}");
                    }
                }
            });
        }
    }
}
