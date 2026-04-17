using JrTools.Dto;
using JrTools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Monitora o retorno dos providers após um ciclo de build/deploy.
    /// Registra qual provider subiu, em quanto tempo e qual versão.
    /// </summary>
    public class ProviderRecoveryTracker
    {
        private readonly ProviderBufferService _bufferService;

        // Evento disparado quando um provider é detectado após o build
        public event EventHandler<ProviderRecoveryResult>? ProviderRecovered;

        public ProviderRecoveryTracker(ProviderBufferService bufferService)
        {
            _bufferService = bufferService;
        }

        /// <summary>
        /// Inicia o rastreamento. Chame logo após o build terminar.
        /// Observa por até <paramref name="timeoutSeconds"/> segundos.
        /// </summary>
        public Task StartAsync(string[] processNames, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => TrackAll(processNames, timeoutSeconds, cancellationToken), cancellationToken);
        }

        private void TrackAll(string[] processNames, int timeoutSeconds, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (var name in processNames)
                tasks.Add(TrackOne(name, timeoutSeconds, cancellationToken));

            Task.WaitAll(tasks.ToArray(), cancellationToken);
        }

        private async Task TrackOne(string processName, int timeoutSeconds, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var deadline = TimeSpan.FromSeconds(timeoutSeconds);

            while (!cancellationToken.IsCancellationRequested && stopwatch.Elapsed < deadline)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    var proc = processes[0];
                    var elapsed = stopwatch.Elapsed;

                    // Tenta ler a versão do buffer compartilhado
                    string version = TryReadVersion(proc.Id);
                    if (string.IsNullOrEmpty(version))
                    {
                        // Buffer ainda não está pronto — aguarda mais um pouco
                        await Task.Delay(1000, cancellationToken);
                        version = TryReadVersion(proc.Id);
                    }

                    ProviderRecovered?.Invoke(this, new ProviderRecoveryResult
                    {
                        ProcessName = processName,
                        Pid = proc.Id,
                        TimeToRecover = elapsed,
                        Version = version,
                        RecoveredAt = DateTime.Now
                    });
                    return;
                }

                try { await Task.Delay(2000, cancellationToken); }
                catch (TaskCanceledException) { return; }
            }

            // Timeout — provider não subiu no tempo esperado
            if (!cancellationToken.IsCancellationRequested)
            {
                ProviderRecovered?.Invoke(this, new ProviderRecoveryResult
                {
                    ProcessName = processName,
                    Pid = -1,
                    TimeToRecover = TimeSpan.FromSeconds(timeoutSeconds),
                    Version = "",
                    RecoveredAt = DateTime.Now,
                    TimedOut = true
                });
            }
        }

        private string TryReadVersion(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                return proc.MainModule?.FileVersionInfo?.FileVersion ?? "";
            }
            catch
            {
                return "";
            }
        }
    }

    public class ProviderRecoveryResult
    {
        public string ProcessName { get; set; } = "";
        public int Pid { get; set; }
        public TimeSpan TimeToRecover { get; set; }
        public string Version { get; set; } = "";
        public DateTime RecoveredAt { get; set; }
        public bool TimedOut { get; set; }

        public string Resumo => TimedOut
            ? $"⚠️ {ProcessName} não subiu em {(int)TimeToRecover.TotalSeconds}s"
            : $"✅ {ProcessName} (PID {Pid}) subiu em {TimeToRecover:mm\\:ss} — v{Version}";
    }
}
