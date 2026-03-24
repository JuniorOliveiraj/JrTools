using JrTools.Dto;
using JrTools.Enums;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class ImportadorRelatoriosPage : Page
    {
        private readonly RelatorioImportService _service = new();
        private List<RelatorioItem> _todosRelatorios = [];
        private ObservableCollection<RelatorioItem> _relatoriosVisiveis = [];
        private ConfiguracaoRelatoriosRh _cfgRh;
        private string _caminhoExe;
        private Dictionary<string, LogDeImportacao> _log = [];
        private bool _mostrarTodos = false;
        private const int LimitePadrao = 10;
        private const int GrauParalelismo = 4;
        private bool _importando = false;
        private bool _carregandoConfig = false;
        private bool _inicializado = false;
        private CancellationTokenSource _cts;

        public ImportadorRelatoriosPage()
        {
            this.InitializeComponent();
            // Mantém a página viva ao navegar — evita cancelamento da importação
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_inicializado) return; // já carregou, não recarrega ao voltar
            _inicializado = true;

            ListaRelatorios.ItemsSource = _relatoriosVisiveis;
            await CarregarConfiguracoesNaTela();
            await CarregarRelatorios();
        }

        private async Task CarregarConfiguracoesNaTela()
        {
            _carregandoConfig = true;
            _cfgRh = await ConfiguracaoRelatoriosHelper.LerAsync();
            var cfg = await ConfigHelper.LerConfiguracoesAsync();
            _caminhoExe = cfg?.CaminhoCSReportImport
                          ?? @"D:\Benner\Servicos\ReportKeeper.V1\CSReportImport.exe";

            TxtCaminhoRelatorios.Text = _cfgRh.CaminhoRelatorios;
            TxtServidor.Text          = _cfgRh.Servidor;
            TxtSistema.Text           = _cfgRh.Sistema;
            TxtUsuario.Text           = _cfgRh.Usuario;
            TxtSenha.Password         = _cfgRh.Senha;
            _carregandoConfig = false;
        }

        private async void TxtConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoConfig || _cfgRh == null) return;
            _cfgRh.CaminhoRelatorios = TxtCaminhoRelatorios.Text;
            _cfgRh.Servidor          = TxtServidor.Text;
            _cfgRh.Sistema           = TxtSistema.Text;
            _cfgRh.Usuario           = TxtUsuario.Text;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        private async void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_carregandoConfig || _cfgRh == null) return;
            _cfgRh.Senha = TxtSenha.Password;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        private async Task CarregarRelatorios()
        {
            LoadingRing.IsActive = true;
            TxtListaVazia.Visibility = Visibility.Collapsed;
            _todosRelatorios.Clear();
            _relatoriosVisiveis.Clear();

            var config = MontarConfiguracaoRh();
            var erros = _service.ValidarConfiguracoes(config);

            if (erros.Count > 0)
            {
                InfoBarAviso.Message = string.Join("\n", erros);
                InfoBarAviso.IsOpen = true;
            }
            else
            {
                InfoBarAviso.IsOpen = false;
                try
                {
                    _log = await _service.LerLogAsync(_cfgRh.Sistema);
                    var lista = await _service.CarregarRelatoriosAsync(
                        _cfgRh.CaminhoRelatorios, _cfgRh.Sistema);

                    _todosRelatorios = lista
                        .OrderBy(r => r.Status == StatusRelatorio.Atualizado ? 1 : 0)
                        .ThenBy(r => r.Nome)
                        .ToList();
                }
                catch (Exception ex)
                {
                    InfoBarAviso.Message = $"Erro ao carregar relatórios: {ex.Message}";
                    InfoBarAviso.IsOpen = true;
                }
            }

            LoadingRing.IsActive = false;
            AplicarFiltro(TxtBusca.Text);
        }

        private void AplicarFiltro(string termo)
        {
            var filtrados = string.IsNullOrWhiteSpace(termo)
                ? _todosRelatorios
                : _todosRelatorios.Where(r => r.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();

            var exibidos = _mostrarTodos ? filtrados : filtrados.Take(LimitePadrao).ToList();

            _relatoriosVisiveis.Clear();
            foreach (var item in exibidos)
                _relatoriosVisiveis.Add(item);

            TxtContador.Text = $"Exibindo {exibidos.Count} de {filtrados.Count} relatórios";
            TxtListaVazia.Visibility = filtrados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            bool temMais = filtrados.Count > LimitePadrao;
            BtnMostrarTodos.Visibility = (!_mostrarTodos && temMais) ? Visibility.Visible : Visibility.Collapsed;
            BtnMostrarTodos.Content = $"Mostrar todos ({filtrados.Count})";
            BtnRecolher.Visibility = (_mostrarTodos && temMais) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnMostrarTodos_Click(object sender, RoutedEventArgs e)
        {
            _mostrarTodos = true;
            AplicarFiltro(TxtBusca.Text);
        }

        private void BtnRecolher_Click(object sender, RoutedEventArgs e)
        {
            _mostrarTodos = false;
            AplicarFiltro(TxtBusca.Text);
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e)
        {
            _mostrarTodos = false;
            AplicarFiltro(TxtBusca.Text);
        }

        private async void BtnAtualizar_Click(object sender, RoutedEventArgs e)
            => await CarregarRelatorios();

        private void ChkSelecionarTodos_Click(object sender, RoutedEventArgs e)
        {
            bool marcar = ChkSelecionarTodos.IsChecked == true;
            foreach (var item in _relatoriosVisiveis)
                item.Selecionado = marcar;

            var temp = _relatoriosVisiveis.ToList();
            _relatoriosVisiveis.Clear();
            foreach (var item in temp)
                _relatoriosVisiveis.Add(item);
        }

        private async void BtnImportarPendentes_Click(object sender, RoutedEventArgs e)
        {
            var pendentes = _todosRelatorios
                .Where(r => r.Status == StatusRelatorio.Novo || r.Status == StatusRelatorio.Diferente)
                .ToList();
            await ExecutarImportacao(pendentes);
        }

        private async void BtnImportarSelecionados_Click(object sender, RoutedEventArgs e)
        {
            var selecionados = _relatoriosVisiveis.Where(r => r.Selecionado).ToList();
            if (selecionados.Count == 0)
            {
                await new ContentDialog
                {
                    Title = "Nenhum relatório selecionado",
                    Content = "Selecione ao menos um relatório na lista antes de importar.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                return;
            }
            await ExecutarImportacao(selecionados);
        }

        private async Task ExecutarImportacao(List<RelatorioItem> relatorios)
        {
            if (_importando) return;

            var config = MontarConfiguracaoRh();
            var erros = _service.ValidarConfiguracoes(config);
            if (erros.Count > 0)
            {
                await new ContentDialog
                {
                    Title = "Configurações inválidas",
                    Content = string.Join("\n", erros),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                return;
            }

            _importando = true;
            _cts = new CancellationTokenSource();
            SetControlesHabilitados(false);
            ProgressoImportacao.Visibility = Visibility.Visible;
            ProgressoImportacao.Maximum = relatorios.Count;
            ProgressoImportacao.Value = 0;
            TxtLog.Text = string.Empty;

            AppendLog($"Início: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            AppendLog($"Processando {relatorios.Count} relatório(s) com até {GrauParalelismo} em paralelo...");

            int sucesso = 0, falha = 0, processados = 0;
            var logLock = new SemaphoreSlim(1, 1);
            var dispatcherQueue = this.DispatcherQueue;

            await Task.Run(async () =>
            {
                await Parallel.ForEachAsync(
                    relatorios,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = GrauParalelismo,
                        CancellationToken = _cts.Token
                    },
                    async (relatorio, ct) =>
                    {
                        ResultadoImportacao resultado;

                        try
                        {
                            resultado = await _service.ImportarRelatorioAsync(relatorio, config);
                        }
                        catch (Exception ex)
                        {
                            resultado = new ResultadoImportacao { Saida = $"ERRO: {ex.Message}", ExitCode = -1 };
                        }

                        await logLock.WaitAsync(ct);
                        try
                        {
                            if (resultado.Sucesso)
                            {
                                // Recalcula hash APÓS importação — o exe pode ter modificado o arquivo
                                var hashPos = _service.CalcularHash(relatorio.Caminho);

                                _log[relatorio.Nome] = new LogDeImportacao
                                {
                                    Nome = relatorio.Nome,
                                    Caminho = relatorio.Caminho,
                                    Hash = hashPos
                                };
                                relatorio.Status = StatusRelatorio.Atualizado;
                                Interlocked.Increment(ref sucesso);

                                // Aviso diagnóstico se saída de texto não contém os termos esperados
                                bool textoIndicaSucesso =
                                    resultado.Saida.Contains("sucesso", StringComparison.OrdinalIgnoreCase) &&
                                    resultado.Saida.Contains("concluída", StringComparison.OrdinalIgnoreCase);
                                if (!textoIndicaSucesso)
                                    dispatcherQueue.TryEnqueue(() =>
                                        AppendLog($"[AVISO] {relatorio.Nome}: exit code 0 mas saída inesperada: {resultado.Saida.Trim()}"));
                            }
                            else
                            {
                                Interlocked.Increment(ref falha);
                            }

                            Interlocked.Increment(ref processados);
                            var linha = $"{relatorio.Nome} -> {resultado.Saida.Trim()}";
                            var prog = processados;

                            dispatcherQueue.TryEnqueue(() =>
                            {
                                AppendLog(linha);
                                ProgressoImportacao.Value = prog;
                            });
                        }
                        finally
                        {
                            logLock.Release();
                        }
                    });
            }, _cts.Token);

            await _service.SalvarLogAsync(_cfgRh.Sistema, _log);

            AppendLog($"Término: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            AppendLog($"Resumo: {sucesso} importado(s) com sucesso, {falha} falha(s).");

            _importando = false;
            _cts.Dispose();
            SetControlesHabilitados(true);
            ProgressoImportacao.Visibility = Visibility.Collapsed;

            // Desmarcar todos e reordenar: pendentes/alterados primeiro, depois atualizados, alfabético em cada grupo
            foreach (var item in _todosRelatorios)
                item.Selecionado = false;

            _todosRelatorios = _todosRelatorios
                .OrderBy(r => r.Status == StatusRelatorio.Atualizado ? 1 : 0)
                .ThenBy(r => r.Nome)
                .ToList();

            _mostrarTodos = false;
            AplicarFiltro(TxtBusca.Text);
        }

        private void AppendLog(string linha)
        {
            TxtLog.Text += linha + "\n";
            ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
        }

        private void SetControlesHabilitados(bool habilitado)
        {
            BtnImportarPendentes.IsEnabled = habilitado;
            BtnImportarSelecionados.IsEnabled = habilitado;
            BtnAtualizar.IsEnabled = habilitado;
            TxtBusca.IsEnabled = habilitado;
            ChkSelecionarTodos.IsEnabled = habilitado;
        }

        private ConfiguracaoRh MontarConfiguracaoRh() => new()
        {
            CaminhoCSReportImport = _caminhoExe ?? string.Empty,
            CaminhoRelatorios     = _cfgRh?.CaminhoRelatorios ?? string.Empty,
            Servidor              = _cfgRh?.Servidor ?? string.Empty,
            Sistema               = _cfgRh?.Sistema ?? "rh",
            Usuario               = _cfgRh?.Usuario ?? string.Empty,
            Senha                 = _cfgRh?.Senha ?? string.Empty
        };
    }
}
