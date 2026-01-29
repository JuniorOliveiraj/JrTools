# Plano de Refatoração: FecharProcessos

## Estado Atual

### O que a página faz:
- **Monitora processos específicos** (BPrv230, CS1, Builder, w3wp) em loop de 5 segundos
- **Exibe quantidade** de processos ativos em ToggleButtons
- **Encerra processos** manualmente (botão único) ou automaticamente (modo "Manter Tudo Fechado")
- **Mostra detalhes** do BPrv230 em DataGrid (PID, linha de comando, RAM, CPU, etc.)
- **Logs em terminal** para ações de encerramento
- Usa **WMI (ManagementObjectSearcher)** para obter linha de comando dos processos

### Problemas Atuais:

1. **Polling Ineficiente**:
   - `Task.Delay(5000)` em loops contínuos
   - Chamadas repetitivas a `Process.GetProcessesByName()` a cada 5 segundos
   - Desperdício de recursos CPU/memória

2. **Código no Code-Behind**:
   - Toda a lógica está em `FecharProcessos.xaml.cs`
   - Dificulta manutenção e testes
   - Sem separação de responsabilidades

3. **Cancelamento de Loops ao Navegar**:
   - `OnNavigatedFrom` **cancela** os loops!
   - Ao voltar para a página, o monitoramento **não retorna automaticamente**
   - Diferente do comportamento desejado (como Copy agora)

4. **Uso de WMI**:
   - `ManagementObjectSearcher` é lento e pesado
   - APIs nativas do Windows seriam mais performáticas

5. **ToggleButtons criados dinamicamente**:
   - Dificulta binding e rastreamento de estado
   - Lógica de UI misturada com lógica de negócio

---

## Objetivos da Refatoração

### 1. Arquitetura MVVM + Singleton
- [ ] Criar `FecharProcessosViewModel` como Singleton (igual Copy)
- [ ] Mover toda lógica de negócio para o ViewModel
- [ ] Manter estado durante navegação

### 2. Monitoramento Nativo Performático
- [ ] Substituir polling por **Event-Driven Monitoring**
- [ ] Usar APIs nativas do Windows:
  - **WMI Events** (`ManagementEventWatcher`) - Mais eficiente que polling
  - **ETW (Event Tracing for Windows)** - Zero overhead (opcional, avançado)
  - **Job Objects** - Para rastreamento de processos filho
- [ ] Implementar **debounce** para evitar atualizações excessivas de UI

### 3. Serviços Especializados
- [ ] Criar `ProcessMonitorService` (monitoramento de processos)
- [ ] Criar `ProcessKillerService` (encerramento de processos)
- [ ] Criar `ProcessInfoService` (obter detalhes de processos)

### 4. Models e DTOs
- [ ] Mover `ProcessoConfigDto` para pasta `Models`
- [ ] Criar `ProcessMonitorConfig` para configuração de monitoramento
- [ ] ViewModel expõe `ObservableCollection<ProcessViewModel>` para binding

### 5. Persistência Durante Navegação
- [ ] ViewModel Singleton persiste estado
- [ ] Monitoramento **NÃO** para ao sair da página
- [ ] Ao voltar, UI reconecta ao ViewModel ativo

---

## Estrutura Proposta

```
JrTools/
├── ViewModels/
│   └── FecharProcessosViewModel.cs (Singleton)
├── Services/
│   ├── ProcessMonitorService.cs
│   ├── ProcessKillerService.cs
│   └── ProcessInfoService.cs
├── Models/
│   ├── ProcessConfig.cs
│   ├── ProcessInfo.cs
│   └── ProcessMonitorConfig.cs
├── Pages/
│   ├── FecharProcessos.xaml
│   └── FecharProcessos.xaml.cs (UI básica)
└── Dto/ (se ainda necessário)
    └── ProcessoDetalhadoDto.cs
```

---

## Implementação Detalhada

### Fase 1: ProcessMonitorService (Event-Driven)

#### Opção A: WMI Event Watcher (Recomendado para começar)
```csharp
public class ProcessMonitorService
{
    private readonly Dictionary<string, ManagementEventWatcher> _watchers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<ProcessEventArgs> ProcessStarted;
    public event EventHandler<ProcessEventArgs> ProcessStopped;

    public void StartMonitoring(string processName)
    {
        // WMI Query para detectar CRIAÇÃO de processos
        var startQuery = new WqlEventQuery(
            "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
            $"WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{processName}.exe'"
        );
        
        var startWatcher = new ManagementEventWatcher(startQuery);
        startWatcher.EventArrived += (s, e) => 
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            int pid = Convert.ToInt32(process["ProcessId"]);
            ProcessStarted?.Invoke(this, new ProcessEventArgs(processName, pid));
        };
        startWatcher.Start();

        // WMI Query para detectar TÉRMINO de processos
        var stopQuery = new WqlEventQuery(
            "SELECT * FROM __InstanceDeletionEvent WITHIN 1 " +
            $"WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{processName}.exe'"
        );
        
        var stopWatcher = new ManagementEventWatcher(stopQuery);
        stopWatcher.EventArrived += (s, e) => 
        {
            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            int pid = Convert.ToInt32(process["ProcessId"]);
            ProcessStopped?.Invoke(this, new ProcessEventArgs(processName, pid));
        };
        stopWatcher.Start();

        _watchers[$"{processName}_start"] = startWatcher;
        _watchers[$"{processName}_stop"] = stopWatcher;
    }

    public void StopMonitoring(string processName)
    {
        if (_watchers.TryGetValue($"{processName}_start", out var startWatcher))
        {
            startWatcher.Stop();
            startWatcher.Dispose();
        }
        if (_watchers.TryGetValue($"{processName}_stop", out var stopWatcher))
        {
            stopWatcher.Stop();
            stopWatcher.Dispose();
        }
    }

    public int GetProcessCount(string processName)
    {
        return Process.GetProcessesByName(processName).Length;
    }

    public Process[] GetProcesses(string processName)
    {
        return Process.GetProcessesByName(processName);
    }
}
```

**Vantagens**:
- ✅ Zero polling (event-driven)
- ✅ Atualizações em **tempo real** (1 segundo ou menos)
- ✅ Muito mais eficiente em CPU
- ✅ Funciona mesmo quando navegando no app

#### Opção B: FileSystemWatcher + ETW (Avançado, máxima performance)
- Usar `EventTraceWatcher` do pacote `Microsoft.Diagnostics.Tracing.TraceEvent`
- Performance nativa do kernel do Windows
- Complexidade maior de implementação

---

### Fase 2: FecharProcessosViewModel (Singleton)

```csharp
public class FecharProcessosViewModel : INotifyPropertyChanged
{
    private static FecharProcessosViewModel _instance;
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
    private DispatcherQueue _dispatcher;

    public ObservableCollection<ProcessViewModel> MonitoredProcesses { get; } = new();
    public ObservableCollection<ProcessDetailViewModel> BPrv230Details { get; } = new();

    private bool _isAutoKillEnabled;
    public bool IsAutoKillEnabled
    {
        get => _isAutoKillEnabled;
        set
        {
            if (_isAutoKillEnabled == value) return;
            _isAutoKillEnabled = value;
            OnPropertyChanged();
            if (value)
                StartAutoKillLoop();
            else
                StopAutoKillLoop();
        }
    }

    private string _logs = "";
    public string Logs
    {
        get => _logs;
        set { _logs = value; OnPropertyChanged(); }
    }

    public ICommand KillProcessCommand { get; }
    public ICommand KillAllCommand { get; }

    private FecharProcessosViewModel()
    {
        _monitorService = new ProcessMonitorService();
        _killerService = new ProcessKillerService();

        // Configurar processos para monitorar
        var processConfigs = new[]
        {
            new ProcessConfig("BPrv230", true),
            new ProcessConfig("CS1", true),
            new ProcessConfig("Builder", false),
            new ProcessConfig("w3wp", true)
        };

        foreach (var config in processConfigs)
        {
            var vm = new ProcessViewModel(config);
            MonitoredProcesses.Add(vm);

            // Event-driven updates
            _monitorService.ProcessStarted += (s, e) => UpdateProcessCount(e.ProcessName);
            _monitorService.ProcessStopped += (s, e) => UpdateProcessCount(e.ProcessName);

            _monitorService.StartMonitoring(config.Name);
        }

        KillProcessCommand = new RelayCommand<ProcessViewModel>(async p => await KillProcessAsync(p));
        KillAllCommand = new RelayCommand(async () => await KillAllProcessesAsync());
    }

    public void InitializeDispatcher()
    {
        if (_dispatcher == null)
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }
    }

    private void UpdateProcessCount(string processName)
    {
        var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
        dispatcher?.TryEnqueue(() =>
        {
            var vm = MonitoredProcesses.FirstOrDefault(p => p.Name == processName);
            if (vm != null)
            {
                vm.Count = _monitorService.GetProcessCount(processName);
            }
        });
    }

    private async Task KillProcessAsync(ProcessViewModel vm)
    {
        await Task.Run(() =>
        {
            _killerService.KillProcessByName(vm.Name);
            AddLog($"⚡ Processos {vm.Name} encerrados");
        });
    }

    private async Task KillAllProcessesAsync()
    {
        foreach (var vm in MonitoredProcesses.Where(p => p.IsEnabled))
        {
            await KillProcessAsync(vm);
        }
    }

    private void AddLog(string message)
    {
        var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
        dispatcher?.TryEnqueue(() =>
        {
            Logs += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            // Limit log size
            if (Logs.Length > 15000)
            {
                Logs = Logs.Substring(Logs.Length - 15000);
            }
        });
    }
}
```

---

### Fase 3: ProcessViewModel (para cada processo)

```csharp
public class ProcessViewModel : INotifyPropertyChanged
{
    private int _count;
    private bool _isEnabled;

    public string Name { get; }
    public bool DefaultEnabled { get; }

    public int Count
    {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public ProcessViewModel(ProcessConfig config)
    {
        Name = config.Name;
        DefaultEnabled = config.EnabledByDefault;
        IsEnabled = DefaultEnabled;
    }
}
```

---

### Fase 4: Atualizar FecharProcessos.xaml

Substituir ToggleButtons dinâmicos por ItemsControl com DataTemplate:

```xml
<ItemsControl ItemsSource="{x:Bind ViewModel.MonitoredProcesses}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    
    <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="vm:ProcessViewModel">
            <ToggleButton 
                FontSize="16"
                Margin="10"
                Width="150"
                Height="150"
                IsChecked="{x:Bind IsEnabled, Mode=TwoWay}">
                <StackPanel>
                    <TextBlock Text="{x:Bind Name}" FontWeight="Bold"/>
                    <TextBlock Text="{x:Bind Count, Mode=OneWay}" FontSize="24"/>
                </StackPanel>
            </ToggleButton>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

---

### Fase 5: Atualizar FecharProcessos.xaml.cs

```csharp
public sealed partial class FecharProcessos : Page
{
    public FecharProcessosViewModel ViewModel { get; }

    public FecharProcessos()
    {
        this.InitializeComponent();
        
        // CRITICAL: Prevent page destruction on navigation
        this.NavigationCacheMode = NavigationCacheMode.Required;
        
        ViewModel = FecharProcessosViewModel.Instance;
        this.DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.InitializeDispatcher();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // NÃO cancela nada - monitoramento continua!
    }
}
```

---

## Comparação: Antes vs Depois

| Aspecto | Antes | Depois |
|---------|-------|--------|
| **Monitoramento** | Polling a cada 5s | Event-driven (WMI Events) |
| **Performance** | Alta CPU/memória | Baixa CPU/memória |
| **Atualização UI** | A cada 5 segundos | Tempo real (< 1s) |
| **Navegação** | Para ao sair | Continua em background |
| **Arquitetura** | Code-behind | MVVM + Singleton |
| **Manutenibilidade** | Difícil | Fácil (separação clara) |
| **Testabilidade** | Impossível | Fácil (ViewModel isolado) |

---

## Cronograma de Implementação

### Sprint 1: Fundação (4-6h)
- [ ] Criar `ProcessMonitorService` com WMI Events
- [ ] Criar `ProcessKillerService`
- [ ] Criar Models (`ProcessConfig`, `ProcessInfo`)
- [ ] Testes unitários dos serviços

### Sprint 2: ViewModel (3-4h)
- [ ] Criar `FecharProcessosViewModel` Singleton
- [ ] Criar `ProcessViewModel`
- [ ] Implementar comandos (Kill, KillAll)
- [ ] Implementar Auto-Kill Loop

### Sprint 3: UI (2-3h)
- [ ] Refatorar XAML com DataBinding
- [ ] Limpar Code-Behind
- [ ] Adicionar `NavigationCacheMode.Required`
- [ ] Testar navegação

### Sprint 4: Polimento (1-2h)
- [ ] Logs aprimorados
- [ ] Tratamento de erros
- [ ] Validação de permissões Admin
- [ ] Documentação

**Total Estimado: 10-15 horas**

---

## Próximos Passos Imediatos

1. ✅ Revisar e aprovar este plano
2. ⏳ Começar pela criação do `ProcessMonitorService`
3. ⏳ Implementar ViewModel Singleton
4. ⏳ Atualizar UI progressivamente
5. ⏳ Testar navegação e persistência

---

## Referências Técnicas

### WMI Event Queries
- [Microsoft Docs: WMI Events](https://docs.microsoft.com/en-us/windows/win32/wmisdk/receiving-a-wmi-event)
- [ManagementEventWatcher Class](https://docs.microsoft.com/en-us/dotnet/api/system.management.managementeventwatcher)

### ETW (Opcional, Advanced)
- [Event Tracing for Windows](https://docs.microsoft.com/en-us/windows/win32/etw/about-event-tracing)
- [Microsoft.Diagnostics.Tracing.TraceEvent](https://github.com/microsoft/perfview/blob/main/documentation/TraceEvent/TraceEventLibrary.md)

### Process Management
- [Process Class](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process)
- [Job Objects](https://docs.microsoft.com/en-us/windows/win32/procthread/job-objects)
