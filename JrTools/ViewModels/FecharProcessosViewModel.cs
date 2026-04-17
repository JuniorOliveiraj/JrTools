using JrTools.Dto;
using JrTools.Models;
using JrTools.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.ViewModels
{
    public class FecharProcessosViewModel : INotifyPropertyChanged
    {
        private static FecharProcessosViewModel? _instance;
        private static readonly object _lock = new object();

        public static FecharProcessosViewModel Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new FecharProcessosViewModel();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly ProcessMonitorService _monitorService;
        private readonly ProcessKillerService _killerService;
        private readonly ProviderBufferService _bufferService;
        private DispatcherQueue? _dispatcher;
        private CancellationTokenSource? _autoKillCts;
        private CancellationTokenSource? _refreshCts;
        private CancellationTokenSource? _providerLogCts;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ProcessViewModel> MonitoredProcesses { get; } = new();
        public ObservableCollection<ProcessInfo> BPrv230Details { get; } = new();
        public ObservableCollection<ProviderInfoItem> SelectedProviderInfo { get; } = new();

        private DeployRecoveryDto? _deployRecovery;
        public DeployRecoveryDto? DeployRecovery
        {
            get => _deployRecovery;
            set { _deployRecovery = value; OnPropertyChanged(); }
        }

        private ProcessInfo? _selectedProvider;
        public ProcessInfo? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (_selectedProvider == value) return;
                _selectedProvider = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedProvider));
                StartProviderLogLoop(value?.PID);
            }
        }

        public bool HasSelectedProvider => _selectedProvider != null;

        private string _selectedProviderLog = "";
        public string SelectedProviderLog
        {
            get => _selectedProviderLog;
            set { _selectedProviderLog = value; OnPropertyChanged(); }
        }

        private ProviderLogType _selectedLogType = ProviderLogType.BDebugAll;
        public ProviderLogType SelectedLogType
        {
            get => _selectedLogType;
            set
            {
                if (_selectedLogType == value) return;
                _selectedLogType = value;
                OnPropertyChanged();
                // Força refresh imediato ao trocar tipo
                if (_selectedProvider != null)
                    _ = RefreshProviderLogAsync(_selectedProvider.PID, value);
            }
        }

        private bool _isAutoKillEnabled;
        public bool IsAutoKillEnabled
        {
            get => _isAutoKillEnabled;
            set
            {
                if (_isAutoKillEnabled == value) return;
                _isAutoKillEnabled = value;
                OnPropertyChanged();
                
                if (value) StartAutoKillLoop();
                else 
                {
                    bool wasActive = _autoKillCts != null; // Verifica se o loop estava rodando
                    StopAutoKillLoop();
                    
                    if (wasActive)
                    {
                        _ = RestartRhAppPoolAsync(); // Só executa se estava realmente ligado
                    }
                }
            }
        }

        private string _logs = "";
        public string Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        private FecharProcessosViewModel()
        {
            _monitorService = new ProcessMonitorService();
            _killerService = new ProcessKillerService();
            _bufferService = new ProviderBufferService();

            var configs = new[]
            {
                new ProcessConfig("BPrv230", true),
                new ProcessConfig("CS1", true),
                new ProcessConfig("Builder", false),
                new ProcessConfig("w3wp", true),

            };

            // Adiciona o controle mestre como o primeiro botão da lista
            MonitoredProcesses.Add(new ProcessViewModel("MASTER_CONTROL", _isAutoKillEnabled) { NameDisplay = "Manter Tudo Fechado" });

            foreach (var config in configs)
            {
                var vm = new ProcessViewModel(config.Name, config.EnabledByDefault) { NameDisplay = config.Name };
                MonitoredProcesses.Add(vm);
                _ = _monitorService.StartMonitoringAsync(config.Name);
            }

            _monitorService.ProcessStarted += (s, e) => UpdateProcessCount(e.ProcessName);
            _monitorService.ProcessStopped += (s, e) => UpdateProcessCount(e.ProcessName);
            _killerService.ProcessKilled += (s, e) => AddLog($"⚡ Processo {e.ProcessName} (PID {e.ProcessId}) encerrado.");
            _killerService.ProcessKillFailed += (s, e) => AddLog($"❌ Erro ao encerrar {e.ProcessName}: {e.ErrorMessage}");

            // Inicia o loop de refresh constante (backup para WMI e atualização de detalhes)
            StartRefreshLoop();
        }

        public void InitializeDispatcher()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            // Sempre que inicializar/voltar para a página, faz um refresh imediato
            RefreshAllNow();
        }

        private void StartRefreshLoop()
        {
            _refreshCts = new CancellationTokenSource();
            _ = RefreshTask(_refreshCts.Token);
        }

        private async Task RefreshTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { RefreshAllNow(); }
                catch { /* nunca deixa o loop morrer */ }

                try { await Task.Delay(5000, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void RefreshAllNow()
        {
            var currentDispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
            if (currentDispatcher == null) return;

            currentDispatcher.TryEnqueue(() =>
            {
                foreach (var vm in MonitoredProcesses)
                {
                    vm.Count = _monitorService.GetProcessCount(vm.Name);
                    
                    if (vm.Name.Equals("BPrv230", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateBPrv230Details();
                    }
                }
            });
        }

        private void UpdateBPrv230Details()
        {
            var processes = _monitorService.GetProcesses("BPrv230");
            var currentPids = processes.Select(p => p.Id).ToHashSet();

            // 1. Remover quem saiu
            var toRemove = BPrv230Details.Where(d => !currentPids.Contains(d.PID)).ToList();
            foreach (var r in toRemove) BPrv230Details.Remove(r);

            // 2. Adicionar/Atualizar
            foreach (var p in processes)
            {
                var existing = BPrv230Details.FirstOrDefault(d => d.PID == p.Id);
                if (existing == null)
                {
                    var info = _monitorService.GetProcessInfo(p.Id);
                    if (info != null) BPrv230Details.Add(info);
                }
                else
                {
                    // Atualiza métricas voláteis se necessário
                    try { existing.TotalProcessorTime = p.TotalProcessorTime; } catch { }
                }
            }
        }

        private void UpdateProcessCount(string processName)
        {
            _dispatcher?.TryEnqueue(() =>
            {
                var vm = MonitoredProcesses.FirstOrDefault(p => p.Name.Equals(processName, StringComparison.OrdinalIgnoreCase));
                if (vm != null)
                {
                    vm.Count = _monitorService.GetProcessCount(processName);
                }
            });
        }

        private void StartAutoKillLoop()
        {
            StopAutoKillLoop();
            _autoKillCts = new CancellationTokenSource();
            _ = AutoKillTask(_autoKillCts.Token);
            AddLog("▶ Modo 'Manter Tudo Fechado' ATIVADO.");
        }

        private void StopAutoKillLoop()
        {
            _autoKillCts?.Cancel();
            _autoKillCts = null;
        }

        private async Task AutoKillTask(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var toKill = MonitoredProcesses
                        .Where(p => p.IsEnabled)
                        .Select(p => p.Name)
                        .ToList();

                    foreach (var name in toKill)
                    {
                        if (token.IsCancellationRequested) break;
                        try
                        {
                            if (_monitorService.GetProcessCount(name) > 0)
                                await _killerService.KillProcessesByNameAsync(name);
                        }
                        catch (Exception ex)
                        {
                            AddLog($"⚠️ Erro ao matar {name}: {ex.Message}");
                        }
                    }

                    try { await Task.Delay(3000, token); }
                    catch (TaskCanceledException) { break; }
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ AutoKillTask encerrado inesperadamente: {ex.Message}");
            }
            AddLog("⏹ Modo 'Manter Tudo Fechado' DESATIVADO.");
        }

        public async Task KillAllNowAsync()
        {
            AddLog("⌛ Encerrando todos os processos selecionados agora...");
            foreach (var vm in MonitoredProcesses.Where(p => p.IsEnabled))
            {
                await _killerService.KillProcessesByNameAsync(vm.Name);
            }
        }

        public async Task KillProviderAsync(int pid)
        {
            AddLog($"⌛ Encerrando provider PID {pid}...");
            var killed = await _killerService.KillProcessByIdAsync(pid);
            if (killed)
            {
                SelectedProvider = null;
                AddLog($"⚡ Provider PID {pid} encerrado.");
            }
        }

        /// <summary>
        /// Chamado pelo RhProdFlow após o build terminar.
        /// Inicia o rastreamento de recovery dos providers e exibe o banner de resultado.
        /// </summary>
        public void IniciarRastreamentoPosDeployAsync(string[] processNames, int timeoutSeconds = 120)
        {
            var buildFinishedAt = DateTime.Now;
            AddLog($"🔍 Aguardando providers subirem após o deploy...");

            var tracker = new ProviderRecoveryTracker(_bufferService);
            var recovery = new DeployRecoveryDto
            {
                BuildFinishedAt = buildFinishedAt,
                IsVisible = true
            };

            tracker.ProviderRecovered += (s, result) =>
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    recovery.Results.Add(result);
                    AddLog(result.Resumo);
                });
            };

            _ = tracker.StartAsync(processNames, timeoutSeconds);
            DeployRecovery = recovery;
        }

        private async Task RestartRhAppPoolAsync()
        {
            AddLog("🌐 Reiniciando AppPool 'Rh' via PowerShell...");
            string namePull =  "Rh";
            try
            {
                await Task.Run(() =>
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Start-WebAppPool -Name '{namePull}'\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas" // Tenta subir como admin se possível
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    process?.WaitForExit();
                });
                AddLog("✅ Comando PowerShell concluído (Restart-WebAppPool Rh).");
            }
            catch (Exception ex)
            {
                AddLog($"❌ Erro ao reiniciar AppPool Rh: {ex.Message}");
            }
        }

        private void StartProviderLogLoop(int? pid)
        {
            _providerLogCts?.Cancel();
            _providerLogCts = null;
            SelectedProviderInfo.Clear();
            SelectedProviderLog = "";

            if (pid == null) return;

            _providerLogCts = new CancellationTokenSource();
            _ = ProviderLogTask(pid.Value, _providerLogCts.Token);
        }

        private async Task ProviderLogTask(int pid, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await RefreshProviderLogAsync(pid, _selectedLogType); }
                catch { /* buffer pode não estar disponível, ignora */ }

                try { await Task.Delay(1500, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RefreshProviderLogAsync(int pid, ProviderLogType logType)
        {
            var snapshot = await Task.Run(() => _bufferService.ReadSnapshot(pid, logType));

            _dispatcher?.TryEnqueue(() =>
            {
                // Atualiza info items (sync por Key)
                var incoming = snapshot.InfoItems;
                var toRemove = SelectedProviderInfo.Where(i => !incoming.Any(x => x.Key == i.Key)).ToList();
                foreach (var r in toRemove) SelectedProviderInfo.Remove(r);

                foreach (var item in incoming)
                {
                    var existing = SelectedProviderInfo.FirstOrDefault(i => i.Key == item.Key);
                    if (existing == null)
                        SelectedProviderInfo.Add(item);
                    else
                        existing.Value = item.Value;
                }

                SelectedProviderLog = snapshot.LogText;
            });
        }

        private void AddLog(string message)
        {
            _dispatcher?.TryEnqueue(() =>
            {
                Logs += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                if (Logs.Length > 15000) Logs = Logs[^15000..];
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
