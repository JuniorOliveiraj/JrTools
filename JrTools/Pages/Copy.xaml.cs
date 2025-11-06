using JrTools.Flows;
using JrTools.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JrTools.Dto;



namespace JrTools.Pages
{
    /// <summary>
    /// Página de gerenciamento de perfis de espelhamento de pastas
    /// </summary>
    public sealed partial class Copy : Page
    {
        #region Constantes
        private const int BATCH_UPDATE_INTERVAL_MS = 250;
        private const int MAX_LOG_LINES = 200;
        #endregion

        #region Propriedades e Campos Estáticos (Sobrevivem à navegação)
        private static PastaEspelhador? _espelhadorGlobal;
        private static PerfilEspelhamento? _perfilEmExecucaoGlobal;
        private static CancellationTokenSource? _cancellationTokenSourceGlobal;
        private static readonly ConcurrentQueue<string> _logQueueGlobal = new();
        private static bool _espelhamentoAtivoGlobal = false;
        #endregion

        #region Propriedades e Campos da Instância
        private ObservableCollection<PerfilEspelhamento> Perfis { get; set; } = new();
        private readonly string _perfisPath = GetConfigPath();
        private List<PerfilEspelhamento> _todosPerfis = new();

        private DispatcherQueueTimer _logTimer;
        #endregion

        #region Construtor e Inicialização
        public Copy()
        {
            InitializeComponent();
            ConfigurarInterface();
            ConfigurarLogTimer();
            ConfigurarEventos();
            _ = CarregarPerfisAsync();

            if (_espelhamentoAtivoGlobal)
            {
                RestaurarEstadoEspelhamento();
            }
        }

        private void ConfigurarInterface()
        {
            PerfisListView.ItemsSource = Perfis;
            IniciarButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }

        private void ConfigurarLogTimer()
        {
            _logTimer = DispatcherQueue.CreateTimer();
            _logTimer.Interval = TimeSpan.FromMilliseconds(BATCH_UPDATE_INTERVAL_MS);
            _logTimer.Tick += ProcessarFilaDeLogs;
            _logTimer.Start();
        }

        private void ConfigurarEventos()
        {
            Unloaded += OnPageUnloaded;
            AdicionarPerfilButton.Click += AdicionarPerfilButton_Click;
            RemoverPerfilButton.Click += RemoverPerfilButton_Click;
            IniciarButton.Click += IniciarButton_Click;
            PerfisListView.SelectionChanged += PerfisListView_SelectionChanged;
            PesquisaInput.TextChanged += PesquisaInput_TextChanged;
        }

        private async void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _logTimer?.Stop();

            // Se estiver espelhando, apenas registrar que continua em background
            if (_espelhamentoAtivoGlobal && _espelhadorGlobal?.EstaExecutando == true && !_espelhadorGlobal.IsDisposed)
            {
                AdicionarLog("ℹ Página fechada. Espelhamento continua em segundo plano.");
            }
            // Se não estiver espelhando ou o espelhador estiver disposed, limpar recursos
            else if (_espelhadorGlobal != null)
            {
                try
                {
                    await LimparRecursosAsync();
                }
                catch
                {
                    // Ignorar erros na limpeza durante unload
                }
            }
        }

        private void RestaurarEstadoEspelhamento()
        {
            if (_perfilEmExecucaoGlobal != null)
            {
                AtualizarUIParaEspelhando();
                AdicionarLog($"ℹ Retornando à página. Espelhamento ativo: '{_perfilEmExecucaoGlobal.Nome}'");

                var perfil = Perfis.FirstOrDefault(p => p.Nome == _perfilEmExecucaoGlobal.Nome);
                if (perfil != null)
                {
                    PerfisListView.SelectedItem = perfil;
                }

                // Ocultar progresso se já passou da fase inicial
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Gerenciamento de Perfis
        private static string GetConfigPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JrTools"
            );

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, "perfilsCopy.json");
        }

        private async Task CarregarPerfisAsync()
        {
            if (!File.Exists(_perfisPath))
            {
                AtualizarPlaceholder();
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(_perfisPath);
                var perfis = await Task.Run(() =>
                    JsonSerializer.Deserialize<List<PerfilEspelhamento>>(json)
                );

                if (perfis != null)
                {
                    _todosPerfis = perfis;
                    AtualizarListaFiltrada();
                }
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao carregar perfis: {ex.Message}");
            }

            AtualizarPlaceholder();
        }

        private async Task SalvarPerfisAsync()
        {
            try
            {
                var json = await Task.Run(() =>
                    JsonSerializer.Serialize(_todosPerfis, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    })
                );

                await File.WriteAllTextAsync(_perfisPath, json);
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao salvar perfis: {ex.Message}");
            }
        }
        #endregion

        #region Controle de Espelhamento
        private async void IniciarButton_Click(object sender, RoutedEventArgs e)
        {
            // Desabilitar o botão temporariamente para evitar cliques múltiplos
            IniciarButton.IsEnabled = false;

            try
            {
                if (EstaEspelhando())
                {
                    await PararEspelhamentoAsync();
                }
                else
                {
                    await IniciarEspelhamentoAsync();
                }
            }
            finally
            {
                // Aguardar um pouco antes de reabilitar
                await Task.Delay(500);

                // Reabilitar o botão apenas se não estiver no meio de uma operação
                if (!EstaEspelhando() && PerfisListView.SelectedItem != null)
                {
                    IniciarButton.IsEnabled = true;
                }
            }
        }

        private bool EstaEspelhando()
        {
            return _espelhamentoAtivoGlobal && _espelhadorGlobal != null && !_espelhadorGlobal.IsDisposed && _espelhadorGlobal.EstaExecutando;
        }

        private async Task IniciarEspelhamentoAsync()
        {
            if (PerfisListView.SelectedItem is not PerfilEspelhamento perfilSelecionado)
            {
                AdicionarLog("❌ Nenhum perfil selecionado para iniciar espelhamento.");
                return;
            }

            if (!Directory.Exists(perfilSelecionado.DiretorioOrigem))
            {
                AdicionarLog($"❌ ERRO: Diretório de origem não existe: {perfilSelecionado.DiretorioOrigem}");
                return;
            }

            // Se já houver algo rodando, limpar primeiro
            if (_espelhadorGlobal != null || _espelhamentoAtivoGlobal)
            {
                AdicionarLog("⚠ Limpando recursos anteriores...");
                await LimparRecursosAsync();
                await Task.Delay(500); // Pequena pausa para garantir limpeza
            }

            try
            {
                _perfilEmExecucaoGlobal = perfilSelecionado;
                _cancellationTokenSourceGlobal = new CancellationTokenSource();
                _espelhadorGlobal = new PastaEspelhador();
                _espelhamentoAtivoGlobal = true;

                await DispatcherQueue.EnqueueAsync(() =>
                {
                    AtualizarUIParaEspelhando();
                    MostrarProgresso();
                });

                AdicionarLog($"🚀 Iniciando espelhamento do perfil '{perfilSelecionado.Nome}'...");

                // Criar progresso que atualiza a barra e os logs
                var progresso = new Progress<ProgressoEspelhamento>(info =>
                {
                    if (info == null) return;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var infoPagina = new ProgressoEspelhamentoCopy
                        {
                            Fase = info.Fase,  // Não precisa de conversão pois agora é o mesmo tipo
                            Percentual = info.Percentual,
                            ArquivosProcessados = info.ArquivosProcessados,
                            TotalArquivos = info.TotalArquivos,
                            ArquivosCopiadados = info.ArquivosCopiadados,
                            Status = info.Status ?? "",
                            Detalhes = info.Detalhes ?? "",
                            Mensagem = info.Mensagem ?? ""
                        };

                        AtualizarBarraDeProgresso(infoPagina);

                        if (!string.IsNullOrWhiteSpace(info.Mensagem))
                        {
                            _logQueueGlobal.Enqueue($"[{DateTime.Now:HH:mm:ss}] {info.Mensagem}");
                        }
                    });
                });

                // Iniciar em background
                await Task.Run(async () =>
                {
                    try
                    {
                        if (_espelhadorGlobal == null || _cancellationTokenSourceGlobal == null || _espelhadorGlobal.IsDisposed)
                        {
                            throw new InvalidOperationException("Recursos não inicializados corretamente");
                        }

                        await _espelhadorGlobal.IniciarEspelhamentoAsync(
                            perfilSelecionado.DiretorioOrigem,
                            perfilSelecionado.DiretorioDestino,
                            progresso,
                            _cancellationTokenSourceGlobal.Token
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        _logQueueGlobal.Enqueue($"[{DateTime.Now:HH:mm:ss}] ⏹ Espelhamento cancelado pelo usuário.");
                    }
                    catch (Exception ex)
                    {
                        _logQueueGlobal.Enqueue($"[{DateTime.Now:HH:mm:ss}] ❌ ERRO CRÍTICO: {ex.Message}");
                    }
                    finally
                    {
                        await DispatcherQueue.EnqueueAsync(async () =>
                        {
                            OcultarProgresso();
                            await LimparRecursosAsync();
                            AtualizarUIParaParado();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AdicionarLog($"❌ Erro ao iniciar espelhamento: {ex.Message}");
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    OcultarProgresso();
                    await LimparRecursosAsync();
                    AtualizarUIParaParado();
                });
            }
        }

        private async Task PararEspelhamentoAsync()
        {
            if (!EstaEspelhando())
            {
                AdicionarLog("⚠ Espelhamento já está parado.");
                return;
            }

            try
            {
                AdicionarLog("⏳ Parando espelhamento...");
                await DispatcherQueue.EnqueueAsync(() =>
                {
                    IniciarButton.IsEnabled = false;
                    IniciarButton.Content = "Parando...";
                });

                // Parar o espelhador com timeout de 5 segundos
                if (_espelhadorGlobal != null && !_espelhadorGlobal.IsDisposed)
                {
                    await _espelhadorGlobal.PararAsync(TimeSpan.FromSeconds(5));
                }

                // Limpar recursos
                await LimparRecursosAsync();
                AdicionarLog("✓ Espelhamento parado com sucesso.");
            }
            catch (Exception ex)
            {
                AdicionarLog($"❌ Erro ao parar espelhamento: {ex.Message}");
                // Forçar limpeza mesmo com erro
                await LimparRecursosAsync();
            }
            finally
            {
                await DispatcherQueue.EnqueueAsync(() =>
                {
                    OcultarProgresso();
                    AtualizarUIParaParado();
                });
            }
        }

        private async Task LimparRecursosAsync()
        {
            try
            {
                // Limpar o espelhador primeiro
                if (_espelhadorGlobal != null)
                {
                    await _espelhadorGlobal.DisposeAsync();
                    _espelhadorGlobal = null;
                }

                // Limpar o token de cancelamento
                if (_cancellationTokenSourceGlobal != null)
                {
                    try
                    {
                        _cancellationTokenSourceGlobal.Dispose();
                    }
                    catch { }
                    _cancellationTokenSourceGlobal = null;
                }

                _perfilEmExecucaoGlobal = null;
                _espelhamentoAtivoGlobal = false;

                // Forçar coleta de lixo para liberar recursos imediatamente
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                AdicionarLog($"❌ Erro ao limpar recursos: {ex.Message}");
            }
        }
        #endregion

        #region Controle de Barra de Progresso
        private void MostrarProgresso()
        {
            ProgressPanel.Visibility = Visibility.Visible;
            SyncProgressBar.Value = 0;
            ProgressPercentText.Text = "0%";
            ProgressStatusText.Text = "Preparando sincronização...";
            ProgressDetailsText.Text = "Analisando arquivos...";
        }

        private void OcultarProgresso()
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }

        private void AtualizarBarraDeProgresso(ProgressoEspelhamentoCopy info)
        {
            // Atualizar barra de progresso
            SyncProgressBar.Value = info.Percentual;
            ProgressPercentText.Text = $"{info.Percentual:F1}%";

            // Atualizar status
            if (!string.IsNullOrWhiteSpace(info.Status))
            {
                ProgressStatusText.Text = info.Status;
            }

            // Atualizar detalhes
            if (info.ArquivosProcessados > 0 && info.TotalArquivos > 0)
            {
                ProgressDetailsText.Text = $"{info.ArquivosProcessados:N0} de {info.TotalArquivos:N0} arquivos";

                if (info.ArquivosCopiadados > 0)
                {
                    ProgressDetailsText.Text += $" ({info.ArquivosCopiadados:N0} copiados)";
                }
            }
            else if (!string.IsNullOrWhiteSpace(info.Detalhes))
            {
                ProgressDetailsText.Text = info.Detalhes;
            }

            // Ocultar progresso quando concluir a sincronização inicial
            if (info.Fase == FaseEspelhamento.MonitoramentoContinuo)
            {
                OcultarProgresso();
            }
        }
        #endregion

        #region Atualização de Interface
        private void AtualizarUIParaEspelhando()
        {
            IniciarButton.Content = "⏹ Parar Espelhamento";
            IniciarButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Red);
            IniciarButton.IsEnabled = true;

            DesabilitarControles();
        }

        private void AtualizarUIParaParado()
        {
            IniciarButton.Content = "▶ Iniciar Espelhamento";
            IniciarButton.Background = (SolidColorBrush)Application.Current.Resources["ButtonBackground"];
            IniciarButton.IsEnabled = PerfisListView.SelectedItem != null;

            HabilitarControles();
        }

        private void DesabilitarControles()
        {
            PerfisListView.IsEnabled = false;
            PesquisaInput.IsEnabled = false;
            AdicionarPerfilButton.IsEnabled = false;
            RemoverPerfilButton.IsEnabled = false;
        }

        private void HabilitarControles()
        {
            PerfisListView.IsEnabled = true;
            PesquisaInput.IsEnabled = true;
            AdicionarPerfilButton.IsEnabled = true;
            RemoverPerfilButton.IsEnabled = true;
        }

        private void PerfisListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!EstaEspelhando())
            {
                IniciarButton.IsEnabled = PerfisListView.SelectedItem != null;
            }
        }
        #endregion

        #region Gerenciamento de Perfis - Adicionar/Remover
        private async void AdicionarPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            LimparCamposDialog();

            var result = await NovoPerfilDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (!ValidarCamposDialog())
                {
                    AdicionarLog("❌ Todos os campos devem ser preenchidos.");
                    return;
                }

                var novoPerfil = CriarNovoPerfilDosInputs();
                _todosPerfis.Add(novoPerfil);

                AtualizarListaFiltrada();
                await SalvarPerfisAsync();
                AtualizarPlaceholder();

                AdicionarLog($"✓ Perfil '{novoPerfil.Nome}' adicionado com sucesso.");
            }
        }

        private void LimparCamposDialog()
        {
            NomePerfilInput.Text = string.Empty;
            OrigemInput.Text = string.Empty;
            DestinoInput.Text = string.Empty;
        }

        private bool ValidarCamposDialog()
        {
            return !string.IsNullOrWhiteSpace(NomePerfilInput.Text) &&
                   !string.IsNullOrWhiteSpace(OrigemInput.Text) &&
                   !string.IsNullOrWhiteSpace(DestinoInput.Text);
        }

        private PerfilEspelhamento CriarNovoPerfilDosInputs()
        {
            return new PerfilEspelhamento
            {
                Nome = NomePerfilInput.Text.Trim(),
                DiretorioOrigem = OrigemInput.Text.Trim(),
                DiretorioDestino = DestinoInput.Text.Trim()
            };
        }

        private async void RemoverPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            if (PerfisListView.SelectedItem is PerfilEspelhamento perfil)
            {
                if (_espelhamentoAtivoGlobal && _perfilEmExecucaoGlobal?.Nome == perfil.Nome)
                {
                    AdicionarLog("❌ Não é possível remover um perfil que está em execução.");
                    return;
                }

                _todosPerfis.Remove(perfil);
                AtualizarListaFiltrada();
                await SalvarPerfisAsync();
                AtualizarPlaceholder();

                AdicionarLog($"🗑 Perfil '{perfil.Nome}' removido.");
            }
            else
            {
                AdicionarLog("❌ Nenhum perfil selecionado para remoção.");
            }
        }
        #endregion

        #region Filtro e Pesquisa
        private void PesquisaInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            AtualizarListaFiltrada();
        }

        private void AtualizarListaFiltrada()
        {
            string filtro = PesquisaInput?.Text?.Trim().ToLower() ?? "";

            var filtrados = string.IsNullOrWhiteSpace(filtro)
                ? _todosPerfis
                : _todosPerfis.Where(p => p.Nome.ToLower().Contains(filtro)).ToList();

            Perfis.Clear();
            foreach (var perfil in filtrados)
            {
                Perfis.Add(perfil);
            }

            AtualizarPlaceholder();
        }

        private void AtualizarPlaceholder()
        {
            EmptyPlaceholder.Visibility = Perfis.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        #endregion

        #region Sistema de Logs
        private void AdicionarLog(string mensagem)
        {
            _logQueueGlobal.Enqueue($"[{DateTime.Now:HH:mm:ss}] {mensagem}");
        }

        private void ProcessarFilaDeLogs(object? sender, object e)
        {
            if (_logQueueGlobal.IsEmpty)
                return;

            var linhas = LogsTextBox.Text.Split('\n').ToList();

            while (_logQueueGlobal.TryDequeue(out var mensagem))
            {
                linhas.Add(mensagem);
            }

            while (linhas.Count > MAX_LOG_LINES)
            {
                linhas.RemoveAt(0);
            }

            LogsTextBox.Text = string.Join('\n', linhas);
            RolarLogsParaFinal();
        }

        private void RolarLogsParaFinal()
        {
            try
            {
                if (VisualTreeHelper.GetChildrenCount(LogsTextBox) > 0)
                {
                    var grid = (Grid)VisualTreeHelper.GetChild(LogsTextBox, 0);

                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
                    {
                        if (VisualTreeHelper.GetChild(grid, i) is ScrollViewer viewer)
                        {
                            viewer.ChangeView(0.0, viewer.ScrollableHeight, 1, true);
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Ignorar erros ao rolar logs
            }
        }
        #endregion
    }

}