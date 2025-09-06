using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public static class ProcessKiller
    { 
        public static Task KillBPrv230Async(IProgress<string>? progresso = null)
            => KillByNameAsync("BPrv230", progresso);

        public static Task KillCs1Async(IProgress<string>? progresso = null)
            => KillByNameAsync("CS1", progresso);

        public static Task KillBuilderAsync(IProgress<string>? progresso = null)
            => KillByNameAsync("Builder", progresso);


        public static Task ListProcessAsync(IProgress<string>? progresso = null)
        {
            return Task.Run(() =>
            {
                foreach (var proc in Process.GetProcesses().OrderBy(p => p.ProcessName))
                {
                    try
                    {
                        progresso?.Report($"[INFO] Processo... [{proc.ProcessName}] - [{proc.Id}]");
                        Console.WriteLine($"Processo {proc.ProcessName} [{proc.Id}]");
                    }
                    catch (Exception ex)
                    {
                        progresso?.Report($"[ERRO] Falha ao listar processos: {ex.Message}");
                        Console.WriteLine($"Erro ao listar processos: {ex.Message}");
                    }
                }
            });
        }

        public static Task<int> QuantidadeDeProcessos(string processName, IProgress<string>? progresso = null)
        {
            int quantidade = 0;

            foreach (var proc in Process.GetProcesses().Where(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    progresso?.Report($"[INFO] Processo... [{proc.ProcessName}] - [{proc.Id}]");
                    Console.WriteLine($"Processo {proc.ProcessName} [{proc.Id}]");
                    quantidade++;
                }
                catch (Exception ex)
                {
                    progresso?.Report($"[ERRO] Falha ao listar processos: {ex.Message}");
                    Console.WriteLine($"Erro ao listar processos: {ex.Message}");
                }
            }

            return Task.FromResult(quantidade);
        }



        public static Task KillByNameAsync(string processName, IProgress<string>? progresso = null)
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
