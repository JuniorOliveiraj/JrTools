using JrTools.Dto;
using JrTools.Models;
using JrTools.Services;
using JrTools.Services.Db;
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
        private bool _customProcessesPendingLoad = true;

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
                        _ = HandleIisRestartFeedbackAsync(); // Só executa se estava realmente ligado
                    }
                }
            }
        }

        public async Task RestartPoolManualAsync()
        {
            await HandleIisRestartFeedbackAsync();
        }

        private async Task HandleIisRestartFeedbackAsync()
        {
            bool success = await RestartRhAppPoolAsync();
            if (success)
            {
                var masterVm = MonitoredProcesses.FirstOrDefault(p => p.Name == "MASTER_CONTROL");
                if (masterVm != null)
                {
                    string originalText = masterVm.NameDisplay;
                    masterVm.NameDisplay = "IIS Reiniciado ✅";
                    await Task.Delay(5000);
                    masterVm.NameDisplay = originalText;
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

            _ = LoadCustomProcessesAsync();
        }

        public void InitializeDispatcher()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            // Sempre que inicializar/voltar para a página, faz um refresh imediato
            RefreshAllNow();

            // Se a primeira tentativa de carregar os processos customizados não conseguiu um
            // dispatcher válido (singleton tocado fora da UI thread, ex.: RhProdFlow antes da
            // página abrir), tenta de novo agora que com certeza estamos na UI thread.
            if (_customProcessesPendingLoad)
                _ = LoadCustomProcessesAsync();
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
        /// Encerra um processo (padrão ou customizado) uma única vez, agora — independente do
        /// estado do toggle ou do "Manter Tudo Fechado". _killerService já dispara ProcessKilled/
        /// ProcessKillFailed, que já estão ligados a AddLog no construtor.
        /// </summary>
        public async Task KillProcessNowAsync(string name)
        {
            AddLog($"⌛ Encerrando {name} agora (uma vez)...");
            await _killerService.KillProcessesByNameAsync(name);
        }

        // ── Processos customizados (adicionados manualmente) ────────────────────

        public Task<List<ProcessoDisponivel>> GetProcessosDisponiveisAsync()
            => Task.Run(() => _monitorService.ListarProcessosDisponiveis());

        public async Task AddCustomProcessAsync(string name)
        {
            if (MonitoredProcesses.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return; // já monitorado (padrão ou customizado) — não duplica

            var vm = new ProcessViewModel(name, enabledByDefault: true, isCustom: true) { NameDisplay = name };
            vm.Count = _monitorService.GetProcessCount(name);
            SubscribeCustomProcessChanges(vm);
            MonitoredProcesses.Add(vm);

            await SaveCustomProcessesAsync();
        }

        public async Task RemoveCustomProcessAsync(string name)
        {
            var vm = MonitoredProcesses.FirstOrDefault(p =>
                p.IsCustom && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (vm == null) return;

            MonitoredProcesses.Remove(vm);
            await SaveCustomProcessesAsync();
        }

        private void SubscribeCustomProcessChanges(ProcessViewModel vm)
        {
            // Persiste o estado ligado/desligado do toggle sempre que mudar — sem isso, um
            // processo customizado restaurado ao reabrir o app sempre voltaria desligado.
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ProcessViewModel.IsEnabled))
                    _ = SaveCustomProcessesAsync();
            };
        }

        private async Task SaveCustomProcessesAsync()
        {
            var config = new ProcessoCustomizadoConfig
            {
                Processos = MonitoredProcesses
                    .Where(p => p.IsCustom)
                    .Select(p => new ProcessoCustomizadoItem { Nome = p.Name, Habilitado = p.IsEnabled })
                    .ToList()
            };
            await ProcessosCustomizadosHelper.SalvarAsync(config);
        }

        private async Task LoadCustomProcessesAsync()
        {
            var config = await ProcessosCustomizadosHelper.LerAsync();

            // Precisa de um dispatcher de verdade pra mexer na ObservableCollection sem
            // derrubar o app — se o singleton foi tocado fora da UI thread (RhProdFlow antes
            // da página abrir), adia e InitializeDispatcher() tenta de novo.
            var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcher == null)
                return;

            _customProcessesPendingLoad = false;
            if (config.Processos.Count == 0) return;

            dispatcher.TryEnqueue(() =>
            {
                foreach (var item in config.Processos)
                {
                    if (MonitoredProcesses.Any(p => p.Name.Equals(item.Nome, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var vm = new ProcessViewModel(item.Nome, item.Habilitado, isCustom: true) { NameDisplay = item.Nome };
                    vm.Count = _monitorService.GetProcessCount(item.Nome);
                    SubscribeCustomProcessChanges(vm);
                    MonitoredProcesses.Add(vm);
                }
            });
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

        private async Task<bool> RestartRhAppPoolAsync()
        {
            var config = await ConfigHelper.LerConfiguracoesAsync();
            string namePull = config?.PoolIisPadrao ?? "Rh";

            AddLog($"🌐 Reiniciando AppPool '{namePull}' via PowerShell...");
            try
            {
                bool success = await Task.Run(() =>
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
                    return process?.ExitCode == 0;
                });

                if (success)
                {
                    AddLog($"✅ Comando PowerShell concluído (Restart-WebAppPool {namePull}).");
                }
                else
                {
                    AddLog($"❌ Falha ao executar comando PowerShell (Restart-WebAppPool {namePull}).");
                }
                return success;
            }
            catch (Exception ex)
            {
                AddLog($"❌ Erro ao reiniciar AppPool {namePull}: {ex.Message}");
                return false;
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
