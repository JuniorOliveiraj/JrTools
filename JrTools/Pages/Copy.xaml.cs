using JrTools.Flows;
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
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        #region Propriedades e Campos
        private ObservableCollection<PerfilEspelhamento> Perfis { get; set; } = new();
        private readonly string _perfisPath = GetConfigPath();
        private List<PerfilEspelhamento> _todosPerfis = new();

        private PastaEspelhador? _espelhador;
        private PerfilEspelhamento? _perfilEmExecucao;
        private CancellationTokenSource? _cancellationTokenSource;

        private readonly ConcurrentQueue<string> _logQueue = new();
        // Altere a declaração do campo _logTimer para remover o modificador 'readonly'
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
        }

        private void ConfigurarInterface()
        {
            PerfisListView.ItemsSource = Perfis;
            IniciarButton.IsEnabled = false;
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
            await PararEspelhamentoAsync();
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
            if (EstaEspelhando())
            {
                await PararEspelhamentoAsync();
            }
            else
            {
                await IniciarEspelhamentoAsync();
            }
        }

        private bool EstaEspelhando()
        {
            return _espelhador != null && _espelhador.EstaExecutando;
        }

        private async Task IniciarEspelhamentoAsync()
        {
            if (PerfisListView.SelectedItem is not PerfilEspelhamento perfilSelecionado)
            {
                AdicionarLog("Nenhum perfil selecionado para iniciar espelhamento.");
                return;
            }

            // Validar diretórios
            if (!Directory.Exists(perfilSelecionado.DiretorioOrigem))
            {
                AdicionarLog($"ERRO: Diretório de origem não existe: {perfilSelecionado.DiretorioOrigem}");
                return;
            }

            try
            {
                _perfilEmExecucao = perfilSelecionado;
                _cancellationTokenSource = new CancellationTokenSource();
                _espelhador = new PastaEspelhador();

                AtualizarUIParaEspelhando();
                AdicionarLog($"Iniciando espelhamento do perfil '{perfilSelecionado.Nome}'...");

                // Criar progresso que despacha para a UI thread
                var progresso = new Progress<string>(msg =>
                {
                    DispatcherQueue.TryEnqueue(() => AdicionarLog(msg));
                });

                // Iniciar em background sem await - deixa rodando
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _espelhador.IniciarEspelhamentoAsync(
                            perfilSelecionado.DiretorioOrigem,
                            perfilSelecionado.DiretorioDestino,
                            progresso,
                            _cancellationTokenSource.Token
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            AdicionarLog("Espelhamento cancelado pelo usuário.")
                        );
                    }
                    catch (Exception ex)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            AdicionarLog($"ERRO CRÍTICO: {ex.Message}")
                        );
                    }
                    finally
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            LimparRecursos();
                            AtualizarUIParaParado();
                        });
                    }
                });

                // Aguardar um pouco para garantir que iniciou
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao iniciar espelhamento: {ex.Message}");
                LimparRecursos();
                AtualizarUIParaParado();
            }
        }

        private async Task PararEspelhamentoAsync()
        {
            if (_espelhador == null || !_espelhador.EstaExecutando)
            {
                AdicionarLog("Espelhamento já está parado.");
                return;
            }

            try
            {
                AdicionarLog("Parando espelhamento...");
                IniciarButton.IsEnabled = false;
                IniciarButton.Content = "Parando...";

                // Cancelar a operação
                _cancellationTokenSource?.Cancel();

                // Parar o espelhador de forma assíncrona
                await Task.Run(() => _espelhador?.Parar());

                // Aguardar um pouco para garantir que tudo foi liberado
                await Task.Delay(300);

                // Limpar recursos
                LimparRecursos();

                AdicionarLog("Espelhamento parado com sucesso.");
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao parar espelhamento: {ex.Message}");
            }
            finally
            {
                AtualizarUIParaParado();
            }
        }

        private void LimparRecursos()
        {
            try
            {
                _espelhador?.Dispose();
                _espelhador = null;

                _perfilEmExecucao = null;

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            catch (Exception ex)
            {
                AdicionarLog($"Erro ao limpar recursos: {ex.Message}");
            }
        }
        #endregion

        #region Atualização de Interface
        private void AtualizarUIParaEspelhando()
        {
            IniciarButton.Content = "Parar Espelhamento";
            IniciarButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Red);
            IniciarButton.IsEnabled = true;

            DesabilitarControles();
        }

        private void AtualizarUIParaParado()
        {
            IniciarButton.Content = "Iniciar Espelhamento";
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
                    AdicionarLog("Todos os campos devem ser preenchidos.");
                    return;
                }

                var novoPerfil = CriarNovoPerfilDosInputs();
                _todosPerfis.Add(novoPerfil);

                AtualizarListaFiltrada();
                await SalvarPerfisAsync();
                AtualizarPlaceholder();

                AdicionarLog($"Perfil '{novoPerfil.Nome}' adicionado com sucesso.");
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
                _todosPerfis.Remove(perfil);
                AtualizarListaFiltrada();
                await SalvarPerfisAsync();
                AtualizarPlaceholder();

                AdicionarLog($"Perfil '{perfil.Nome}' removido.");
            }
            else
            {
                AdicionarLog("Nenhum perfil selecionado para remoção.");
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
            _logQueue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {mensagem}");
        }

        private void ProcessarFilaDeLogs(object? sender, object e)
        {
            if (_logQueue.IsEmpty)
                return;

            var linhas = LogsTextBox.Text.Split('\n').ToList();

            // Processar todos os logs da fila
            while (_logQueue.TryDequeue(out var mensagem))
            {
                linhas.Add(mensagem);
            }

            // Limitar o número de linhas
            while (linhas.Count > MAX_LOG_LINES)
            {
                linhas.RemoveAt(0);
            }

            LogsTextBox.Text = string.Join('\n', linhas);

            // Rolar para o final
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

    /// <summary>
    /// Representa um perfil de espelhamento de diretórios
    /// </summary>
    public class PerfilEspelhamento
    {
        public string Nome { get; set; } = "";
        public string DiretorioOrigem { get; set; } = "";
        public string DiretorioDestino { get; set; } = "";
    }
}