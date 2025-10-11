using CommunityToolkit.WinUI;
using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class FecharProcessos : Page
    {
        private const int MAX_TERMINAL_LENGTH = 15000;

        private CancellationTokenSource? _processoLoopCts;
        private CancellationTokenSource? _quantidadeLoopCts;

        private readonly ObservableCollection<ProcessoDetalhadoDto> _processosBPrv230 = new();
        private readonly Dictionary<int, StringBuilder> _logsProcessos = new();

        private readonly ProcessoConfigDto[] _processos =
        {
            new ProcessoConfigDto("BPrv230", true),
            new ProcessoConfigDto("CS1", true),
            new ProcessoConfigDto("Builder", false),
            new ProcessoConfigDto("w3wp", true)
        };

        private bool _valoresCarregados = false;

        public FecharProcessos()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += Page_Loaded;

            ProcessosDataGrid.ItemsSource = _processosBPrv230;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_valoresCarregados)
                return;

            _valoresCarregados = true;

            if (!EstaRodandoComoAdministrador())
            {
                await MostrarMensagemAdminNecessario();
            }

            CriarToggleButtonsDinamicamente();

            _quantidadeLoopCts = new CancellationTokenSource();
            _ = AtualizarQuantidadesLoop(_quantidadeLoopCts.Token);
        }

        private void CriarToggleButtonsDinamicamente()
        {
            foreach (var proc in _processos)
            {
                var toggle = new ToggleButton
                {
                    FontSize = 16,
                    Margin = new Thickness(10),
                    Width = 150,
                    Height = 150,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = proc.AtivoPorPadrao,
                    Content = new TextBlock
                    {
                        Text = $"{proc.Nome}\n0",
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                ProcessosItemsControl.Items.Add(toggle);
            }
        }

        // Botão "Manter Tudo Fechado"
        private void ManterTudoFechadoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManterTudoFechadoToggleButton.IsChecked == true)
            {
                _processoLoopCts = new CancellationTokenSource();
                _ = LoopKillTodosProcessos(_processoLoopCts.Token);
            }
            else
            {
                _processoLoopCts?.Cancel();
            }
        }

        // Botão "Fechar Tudo" imediato
        private async void FecharTudoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EstaRodandoComoAdministrador())
            {
                await MostrarMensagemAdminNecessario();
                return;
            }

            foreach (var proc in _processos)
            {
                await KillProcessosPorNome(proc.Nome);
                // *** ATUALIZAÇÃO IMEDIATA ***
                var toggle = ProcessosItemsControl.Items
                                 .OfType<ToggleButton>()
                                 .FirstOrDefault(t => ExtrairNomeDoToggle(t) == proc.Nome);
                if (toggle != null)
                {
                    AtualizarTextoToggle(toggle, proc.Nome, 0);
                }
            }
        }

        private async Task LoopKillTodosProcessos(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                bool adminModalExibido = false;

                while (!token.IsCancellationRequested)
                {
                    var processosParaMatar = new List<string>();
                    bool isLoopActive = false;

                    await DispatcherQueue.EnqueueAsync(() =>
                    {
                        isLoopActive = ManterTudoFechadoToggleButton.IsChecked == true;
                        if (!isLoopActive) return;

                        foreach (var item in ProcessosItemsControl.Items)
                        {
                            if (item is ToggleButton toggle && toggle.IsChecked == true && toggle != ManterTudoFechadoToggleButton)
                            {
                                processosParaMatar.Add(ExtrairNomeDoToggle(toggle));
                            }
                        }
                    });

                    if (!isLoopActive) break;

                    if (!EstaRodandoComoAdministrador())
                    {
                        if (!adminModalExibido)
                        {
                            adminModalExibido = true;
                            DispatcherQueue.TryEnqueue(async () => await MostrarMensagemAdminNecessario());
                        }
                        await Task.Delay(5000, token);
                        continue;
                    }

                    foreach (var nomeProcesso in processosParaMatar)
                    {
                        await KillProcessosPorNome(nomeProcesso);
                    }

                    // *** ATUALIZAÇÃO IMEDIATA ***
                    // Força a atualização da contagem para os processos que acabaram de ser encerrados.
                    await DispatcherQueue.EnqueueAsync(() =>
                    {
                        foreach (var nomeProcesso in processosParaMatar)
                        {
                            var toggle = ProcessosItemsControl.Items
                                             .OfType<ToggleButton>()
                                             .FirstOrDefault(t => ExtrairNomeDoToggle(t) == nomeProcesso);
                            if (toggle != null)
                            {
                                AtualizarTextoToggle(toggle, nomeProcesso, 0);
                            }
                        }
                    });

                    try
                    {
                        await Task.Delay(5000, token);
                    }
                    catch (TaskCanceledException) { break; }
                }
            }, token);
        }


        private async Task KillProcessosPorNome(string nomeProcesso)
        {
            await Task.Run(() =>
            {
                var processos = Process.GetProcessesByName(nomeProcesso);
                foreach (var proc in processos)
                {
                    try
                    {
                        proc.Kill(true);
                        AppendTerminalLog($"⚡ Processo {nomeProcesso} (PID {proc.Id}) encerrado", proc.Id);
                    }
                    catch
                    {
                        AppendTerminalLog($"❌ Não foi possível encerrar {nomeProcesso} (PID {proc.Id})", proc.Id);
                    }
                }
            });
        }

        private async Task AtualizarQuantidadesLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var procConfig in _processos)
                {
                    string nomeProcesso = procConfig.Nome;
                    var processosAtivos = await Task.Run(() => Process.GetProcessesByName(nomeProcesso));
                    int qtd = processosAtivos.Length;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var toggle = ProcessosItemsControl.Items
                                         .OfType<ToggleButton>()
                                         .FirstOrDefault(t => ExtrairNomeDoToggle(t) == nomeProcesso);
                        if (toggle != null)
                        {
                            AtualizarTextoToggle(toggle, nomeProcesso, qtd);
                        }

                        if (nomeProcesso == "BPrv230")
                        {
                            AtualizarDetalhesBPrv230(processosAtivos);
                        }
                    });
                }

                try { await Task.Delay(5000, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void AtualizarDetalhesBPrv230(Process[] processosBrutos)
        {
            var pidsAtuais = processosBrutos.Select(p => p.Id).ToHashSet();
            var removeList = _processosBPrv230.Where(p => !pidsAtuais.Contains(p.PID)).ToList();
            foreach (var p in removeList) _processosBPrv230.Remove(p);

            foreach (var proc in processosBrutos)
            {
                if (_processosBPrv230.Any(p => p.PID == proc.Id)) continue;

                var extraInfo = GetProcessExtraInfo(proc.Id);
                _processosBPrv230.Add(new ProcessoDetalhadoDto
                {
                    PID = proc.Id,
                    NomeProcesso = proc.ProcessName,
                    LinhaComando = extraInfo.commandLine ?? "N/A",
                    Host = Environment.MachineName,
                    TempoCPU = proc.TotalProcessorTime.ToString(@"hh\:mm\:ss"),
                    MemoriaRamMB = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 2),
                    HoraInicio = proc.StartTime.ToString("dd/MM/yyyy HH:mm:ss")
                });
            }
        }

        private static string ExtrairNomeDoToggle(ToggleButton toggle)
        {
            if (toggle.Content is TextBlock tb)
                return tb.Text.Split('\n')[0];
            return toggle.Content?.ToString() ?? "";
        }

        private static void AtualizarTextoToggle(ToggleButton toggle, string nomeProcesso, int quantidade)
        {
            if (toggle.Content is TextBlock tb)
            {
                tb.Text = $"{nomeProcesso}\n{quantidade}";
            }
            else
            {
                toggle.Content = new TextBlock
                {
                    Text = $"{nomeProcesso}\n{quantidade}",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private void ProcessosDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProcessosDataGrid.SelectedItem is ProcessoDetalhadoDto processoSelecionado)
            {
                MatarProcessoButton.IsEnabled = true;

                if (_logsProcessos.TryGetValue(processoSelecionado.PID, out var sb))
                    TerminalOutput.Text = sb.ToString();
                else
                    TerminalOutput.Text = processoSelecionado.LogCompleto ?? "Nenhum log disponível.";

                TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
            }
            else
            {
                MatarProcessoButton.IsEnabled = false;
            }
        }

        private async void MatarProcessoButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessosDataGrid.SelectedItem is ProcessoDetalhadoDto processoSelecionado)
            {
                if (!EstaRodandoComoAdministrador())
                {
                    await MostrarMensagemAdminNecessario();
                    return;
                }
                string nomeProcesso = processoSelecionado.NomeProcesso;
                await KillProcessosPorNome(nomeProcesso);

                // *** ATUALIZAÇÃO IMEDIATA ***
                var toggle = ProcessosItemsControl.Items
                                 .OfType<ToggleButton>()
                                 .FirstOrDefault(t => ExtrairNomeDoToggle(t) == nomeProcesso);
                if (toggle != null)
                {
                    AtualizarTextoToggle(toggle, nomeProcesso, 0);
                }
            }
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _quantidadeLoopCts?.Cancel();
            _processoLoopCts?.Cancel();
            base.OnNavigatedFrom(e);
        }

        private void AppendTerminalLog(string mensagem, int? pid = null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (pid == null)
                {
                    TerminalOutput.Text += mensagem + "\n";
                    if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
                        TerminalOutput.Text = TerminalOutput.Text[^MAX_TERMINAL_LENGTH..];

                    TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
                }
                else
                {
                    if (!_logsProcessos.ContainsKey(pid.Value))
                        _logsProcessos[pid.Value] = new StringBuilder();

                    var sb = _logsProcessos[pid.Value];
                    sb.AppendLine(mensagem);
                    if (sb.Length > MAX_TERMINAL_LENGTH)
                        sb.Remove(0, sb.Length - MAX_TERMINAL_LENGTH);

                    if (ProcessosDataGrid.SelectedItem is ProcessoDetalhadoDto sel && sel.PID == pid.Value)
                    {
                        TerminalOutput.Text = sb.ToString();
                        TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
                    }
                }
            });
        }

        private static (string? owner, string? commandLine) GetProcessExtraInfo(int pid)
        {
            try
            {
                string query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}";
                using var searcher = new ManagementObjectSearcher(query);
                using var results = searcher.Get();
                var mo = results.Cast<ManagementObject>().FirstOrDefault();
                string cmd = mo?["CommandLine"]?.ToString();
                return ("Acesso negado", cmd);
            }
            catch { return ("Acesso negado", "Acesso negado"); }
        }

        private bool EstaRodandoComoAdministrador()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private async Task MostrarMensagemAdminNecessario()
        {
            var dialog = new ContentDialog
            {
                Title = "Privilégios de Administrador",
                Content = "Para encerrar alguns processos do sistema, este aplicativo precisa ser executado como Administrador.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}

