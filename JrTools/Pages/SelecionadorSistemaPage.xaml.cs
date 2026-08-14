using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace JrTools.Pages
{
    public sealed partial class SelecionadorSistemaPage : Page
    {
        private SelecionadorSistemaConfig _cfg = new();
        private string _diretorioBinarios = string.Empty;
        private bool _carregando = false;
        private CancellationTokenSource _ctsBusca;
        private bool _atualizandoCredenciais = false;
        private string _aplicativo = "Runner";
        private string _parametrosExtras = string.Empty;
        private List<string> _todosSistemas = new();
        private readonly ObservableCollection<ServidorStatusDto> _servidores = new();

        public SelecionadorSistemaPage()
        {
            InitializeComponent();
            ListServidores.ItemsSource = _servidores;
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += OnLoaded;
        }

        private void CardsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Garante que o container sempre ocupe pelo menos a altura visível do ScrollViewer.
            // Em tela grande → sem scroll, cards preenchem 100%.
            // Em tela pequena → conteúdo ultrapassa MinHeight → scrollbar aparece.
            CardsContainer.MinHeight = ((ScrollViewer)sender).ViewportHeight;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try { await CarregarAsync(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SELECIONADOR] Erro em OnLoaded: {ex}");
            }
        }

        private async Task CarregarAsync()
        {
            _carregando = true;

            var mainCfg = await ConfigHelper.LerConfiguracoesAsync();
            _diretorioBinarios = mainCfg?.DiretorioBinarios ?? string.Empty;
            _cfg = await SelecionadorSistemaHelper.LerAsync();

            // Popula lista de servidores
            _servidores.Clear();
            foreach (var nome in _cfg.ServidoresRecentes)
                _servidores.Add(new ServidorStatusDto { Nome = nome });


            // Restaura aplicativo
            _aplicativo = _cfg.UltimoAplicativo switch
            {
                "Builder"    => "Builder",
                "BuilderCli" => "BuilderCli",
                "Instaler"   => "Instaler",
                _            => "Runner"
            };
            CmbAplicativo.SelectedIndex = _aplicativo switch
            {
                "Builder"    => 1,
                "BuilderCli" => 2,
                "Instaler"   => 3,
                _            => 0
            };

            // Carrega pastas de binários
            await CarregarPastasAsync();

            // Restaura último servidor + sistemas + credenciais
            if (!string.IsNullOrWhiteSpace(_cfg.UltimoServidor))
            {
                var srvDto = _servidores.FirstOrDefault(s => s.Nome == _cfg.UltimoServidor);
                if (srvDto != null)
                {
                    // Restaura cache de sistemas sem conectar
                    RestaurarSistemasDoCache(_cfg.UltimoServidor);
                    ListServidores.SelectedItem = srvDto;
                }
            }

            _carregando = false;
            AtualizarComando();

            // Verifica status TCP de todos os servidores em background
            foreach (var srv in _servidores.ToList())
                _ = VerificarStatusAsync(srv);
        }

        // ── Pastas de binários ─────────────────────────────────────────────────

        private async Task CarregarPastasAsync()
        {
            if (string.IsNullOrWhiteSpace(_diretorioBinarios) || !Directory.Exists(_diretorioBinarios))
            {
                ListBinarios.ItemsSource = null;
                return;
            }
            try
            {
                var pastas = Folders.ListarPastas(_diretorioBinarios);
                var items = await Task.Run(() => pastas.Select(p => new PastaBinariosDto
                {
                    Nome    = p.Nome,
                    Caminho = p.Caminho,
                    Versao  = ObterVersaoBenner(p.Caminho)
                }).ToList());

                ListBinarios.ItemsSource = items;

                if (!string.IsNullOrWhiteSpace(_cfg.UltimaPasta))
                {
                    var match = items.FirstOrDefault(p => p.Nome == _cfg.UltimaPasta);
                    if (match != null) ListBinarios.SelectedItem = match;
                }
            }
            catch { }
        }

        private static string ObterVersaoBenner(string caminhoPasta)
        {
            var candidatos = new[] { "DOServer.dll", "Benner.Tecnologia.Common.dll", "CS1.exe", "Builder.exe", "CS.exe" };
            foreach (var nome in candidatos)
            {
                var path = Path.Combine(caminhoPasta, nome);
                if (!File.Exists(path)) continue;
                try
                {
                    var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                    if (fvi.FileMajorPart > 0)
                        return $"{fvi.FileMajorPart:D2}.{fvi.FileMinorPart:D2}";
                }
                catch { }
            }
            return string.Empty;
        }

        private async void BtnRecarregarPastas_Click(object sender, RoutedEventArgs e)
            => await CarregarPastasAsync();

        private void ListBinarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregando) return;
            if (ListBinarios.SelectedItem is PastaBinariosDto pasta)
            {
                _cfg.UltimaPasta = pasta.Nome;
                _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
            }
            AtualizarComando();
        }

        // ── Aplicativo ─────────────────────────────────────────────────────────

        private void CmbAplicativo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAplicativo.SelectedItem is ComboBoxItem item)
                _aplicativo = item.Tag as string ?? "Runner";
            if (_carregando) return;
            _cfg.UltimoAplicativo = _aplicativo;
            _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
            AtualizarComando();
        }

        // ── Servidores ─────────────────────────────────────────────────────────

        private void TxtNovoServidor_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) AdicionarServidor();
        }

        private void BtnAdicionarServidor_Click(object sender, RoutedEventArgs e)
            => AdicionarServidor();

        private void AdicionarServidor()
        {
            var nome = TxtNovoServidor.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nome)) return;
            if (_servidores.Any(s => s.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase)))
            {
                // Já existe — só seleciona
                var existente = _servidores.First(s => s.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
                ListServidores.SelectedItem = existente;
                TxtNovoServidor.Text = string.Empty;
                return;
            }

            var dto = new ServidorStatusDto { Nome = nome };
            _servidores.Insert(0, dto);

            _cfg.ServidoresRecentes.RemoveAll(s => s.Equals(nome, StringComparison.OrdinalIgnoreCase));
            _cfg.ServidoresRecentes.Insert(0, nome);
            if (_cfg.ServidoresRecentes.Count > 20) _cfg.ServidoresRecentes.RemoveAt(20);

            TxtNovoServidor.Text = string.Empty;

            // Seleciona e conecta automaticamente
            ListServidores.SelectedItem = dto;

            _ = VerificarStatusAsync(dto);
            _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
        }

        private void BtnRemoverServidorSelecionado_Click(object sender, RoutedEventArgs e)
        {
            if (ListServidores.SelectedItem is not ServidorStatusDto dto) return;

            _servidores.Remove(dto);
            _cfg.ServidoresRecentes.RemoveAll(s => s == dto.Nome);

            // Limpa sistemas se era o servidor selecionado
            _todosSistemas.Clear();
            ListSistemas.ItemsSource = null;
            InfoBarBServer.IsOpen = false;
            TxtFiltroSistema.Text = string.Empty;

            _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
            AtualizarComando();
        }

        private async Task VerificarStatusAsync(ServidorStatusDto dto)
        {
            DispatcherQueue.TryEnqueue(() => dto.Status = StatusServidor.Verificando);
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(dto.Nome, 5337).WaitAsync(TimeSpan.FromSeconds(3));
                DispatcherQueue.TryEnqueue(() => dto.Status = StatusServidor.Online);
            }
            catch
            {
                // Não sobrescreve se ConectarECarregarSistemasAsync já confirmou Online
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (dto.Status != StatusServidor.Online)
                        dto.Status = StatusServidor.Offline;
                });
            }
        }

        private async void ListServidores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregando) return;
            if (ListServidores.SelectedItem is not ServidorStatusDto dto) return;

            _cfg.UltimoServidor = dto.Nome;

            // Cancela busca anterior e cria novo token
            _ctsBusca?.Cancel();
            _ctsBusca?.Dispose();
            _ctsBusca = new CancellationTokenSource();
            var token = _ctsBusca.Token;

            // Limpa tudo do servidor anterior antes de qualquer coisa
            _todosSistemas.Clear();
            ListSistemas.ItemsSource = null;
            TxtFiltroSistema.Text = string.Empty;
            TxtUsuario.Text = string.Empty;
            TxtSenha.Text = string.Empty;
            AtualizarComando();

            // Mostra cache do novo servidor imediatamente enquanto conecta (se houver)
            RestaurarSistemasDoCache(dto.Nome);

            // Conecta para atualizar lista
            await ConectarECarregarSistemasAsync(dto, token);
        }

        private void RestaurarSistemasDoCache(string servidor)
        {
            if (!_cfg.Servidores.TryGetValue(servidor, out var hist) || hist.Sistemas.Count == 0) return;

            _todosSistemas = new List<string>(hist.Sistemas);
            TxtFiltroSistema.Text = string.Empty;
            ListSistemas.ItemsSource = _todosSistemas;

            if (!string.IsNullOrWhiteSpace(hist.UltimoSistema))
            {
                var idx = _todosSistemas.FindIndex(s => s == hist.UltimoSistema);
                if (idx >= 0) ListSistemas.SelectedIndex = idx;
                PreencherCredenciais(servidor, hist.UltimoSistema);
            }
        }

        private async Task ConectarECarregarSistemasAsync(ServidorStatusDto dto, CancellationToken token)
        {
            LoadingSistemas.IsActive = true;
            InfoBarBServer.IsOpen = false;
            dto.Status = StatusServidor.Verificando;

            var servidor = dto.Nome;
            var sistemaAtual = ListSistemas.SelectedItem as string ?? _cfg.UltimoSistema;

            var resultado = await BServerQueryService.ConsultarAsync(servidor, _diretorioBinarios);

            // Se o usuário já trocou de servidor, descarta este resultado
            if (token.IsCancellationRequested)
            {
                LoadingSistemas.IsActive = false;
                return;
            }

            if (resultado.IsSuccess)
            {
                _todosSistemas = resultado.AvailableSystems.ToList();
                TxtFiltroSistema.Text = string.Empty;
                ListSistemas.ItemsSource = _todosSistemas;

                var idx = _todosSistemas.FindIndex(s =>
                    string.Equals(s, sistemaAtual, StringComparison.OrdinalIgnoreCase));
                ListSistemas.SelectedIndex = idx >= 0 ? idx : (_todosSistemas.Count > 0 ? 0 : -1);

                if (!_cfg.Servidores.ContainsKey(servidor))
                    _cfg.Servidores[servidor] = new ServidorHistorico();
                _cfg.Servidores[servidor].Sistemas = _todosSistemas;

                _cfg.ServidoresRecentes.RemoveAll(s => s == servidor);
                _cfg.ServidoresRecentes.Insert(0, servidor);
                if (_cfg.ServidoresRecentes.Count > 20) _cfg.ServidoresRecentes.RemoveAt(20);

                await SelecionadorSistemaHelper.SalvarAsync(_cfg);

                InfoBarBServer.Message  = $"{_todosSistemas.Count} sistema(s) encontrado(s)";
                InfoBarBServer.Severity = InfoBarSeverity.Success;
                InfoBarBServer.IsOpen   = true;

                DispatcherQueue.TryEnqueue(() => dto.Status = StatusServidor.Online);
            }
            else
            {
                _todosSistemas.Clear();
                ListSistemas.ItemsSource = null;
                TxtFiltroSistema.Text = string.Empty;
                TxtUsuario.Text = string.Empty;
                TxtSenha.Text = string.Empty;

                InfoBarBServer.Message  = resultado.ErrorMessage;
                InfoBarBServer.Severity = InfoBarSeverity.Error;
                InfoBarBServer.IsOpen   = true;

                DispatcherQueue.TryEnqueue(() => dto.Status = StatusServidor.Offline);
            }

            LoadingSistemas.IsActive = false;
        }

        private void PreencherCredenciais(string servidor, string sistema)
        {
            if (!_cfg.Servidores.TryGetValue(servidor, out var srvHist)) return;
            if (!srvHist.HistoricoSistemas.TryGetValue(sistema, out var sisHist)) return;
            _atualizandoCredenciais = true;
            TxtUsuario.Text = sisHist.UltimoUsuario;
            TxtSenha.Text   = sisHist.UltimaSenha;
            _atualizandoCredenciais = false;
        }

        private void SalvarCredenciaisAtuais()
        {
            if (_atualizandoCredenciais || _carregando) return;
            var servidor = (ListServidores.SelectedItem as ServidorStatusDto)?.Nome;
            var sistema  = ListSistemas.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(servidor) || string.IsNullOrWhiteSpace(sistema)) return;

            if (!_cfg.Servidores.ContainsKey(servidor))
                _cfg.Servidores[servidor] = new ServidorHistorico();
            var srvH = _cfg.Servidores[servidor];
            if (!srvH.HistoricoSistemas.ContainsKey(sistema))
                srvH.HistoricoSistemas[sistema] = new SistemaHistorico();
            srvH.HistoricoSistemas[sistema].UltimoUsuario = TxtUsuario.Text?.Trim() ?? string.Empty;
            srvH.HistoricoSistemas[sistema].UltimaSenha   = TxtSenha.Text ?? string.Empty;

            _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
        }

        // ── Filtro e sistemas ──────────────────────────────────────────────────

        private void TxtFiltroSistema_TextChanged(object sender, TextChangedEventArgs e)
        {
            var termo = TxtFiltroSistema.Text;
            ListSistemas.ItemsSource = string.IsNullOrWhiteSpace(termo)
                ? _todosSistemas
                : _todosSistemas.Where(s => s.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ListSistemas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregando) return;
            if (ListSistemas.SelectedItem is string sistema)
            {
                _cfg.UltimoSistema = sistema;

                var servidor = (ListServidores.SelectedItem as ServidorStatusDto)?.Nome;
                if (!string.IsNullOrWhiteSpace(servidor))
                {
                    if (!_cfg.Servidores.ContainsKey(servidor))
                        _cfg.Servidores[servidor] = new ServidorHistorico();
                    _cfg.Servidores[servidor].UltimoSistema = sistema;
                    PreencherCredenciais(servidor, sistema);
                }

                _ = SelecionadorSistemaHelper.SalvarAsync(_cfg);
            }
            AtualizarComando();
        }

        // ── Credenciais ────────────────────────────────────────────────────────

        private void TxtUsuario_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando) return;
            SalvarCredenciaisAtuais();
            AtualizarComando();
        }

        private void TxtSenha_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregando) return;
            SalvarCredenciaisAtuais();
            AtualizarComando();
        }

        // ── Comando ────────────────────────────────────────────────────────────

        private void AtualizarComando()
        {
            if (TxtComando is null) return;
            var cmd = MontarComando();
            TxtComando.Text   = cmd ?? string.Empty;
            var ok            = !string.IsNullOrWhiteSpace(cmd);
            BtnCopiar.IsEnabled  = ok;
            BtnExecutar.IsEnabled = ok;
        }

        private string MontarComando()
        {
            var pasta    = ListBinarios.SelectedItem as PastaBinariosDto;
            var sistema  = ListSistemas.SelectedItem as string;
            var servidor = (ListServidores.SelectedItem as ServidorStatusDto)?.Nome;
            var usuario  = TxtUsuario.Text?.Trim();
            var senha    = TxtSenha.Text;

            if (pasta == null || string.IsNullOrWhiteSpace(sistema) ||
                string.IsNullOrWhiteSpace(servidor)) return null;

            var exeNome = _aplicativo switch
            {
                "Builder"    => "Builder.exe",
                "BuilderCli" => "BuilderCli.exe",
                "Instaler"   => "CS.exe",
                _            => "CS1.exe"
            };
            var exePath = Path.Combine(pasta.Caminho, exeNome);

            var extras = string.IsNullOrWhiteSpace(_parametrosExtras) ? "" : $" {_parametrosExtras}";
            return _aplicativo switch
            {
                "Instaler" => $"\"{exePath}\" {servidor} 5342 {sistema} -pu {usuario} -pp {senha}{extras}",
                "Runner"   => $"\"{exePath}\" {sistema}@{servidor} -run -pu {usuario} -pp {senha}{extras}",
                _          => $"\"{exePath}\" {sistema}@{servidor} -pu {usuario} -pp {senha}{extras}",
            };
        }

        // ── Ações ──────────────────────────────────────────────────────────────

        private async void BtnParametros_Click(object sender, RoutedEventArgs e)
        {
            TxtParametrosExtras.Text = _parametrosExtras;
            ParametrosDialog.XamlRoot = XamlRoot;
            var resultado = await ParametrosDialog.ShowAsync();
            if (resultado != ContentDialogResult.Primary) return;

            _parametrosExtras = TxtParametrosExtras.Text?.Trim() ?? string.Empty;
            AtualizarBadgeParams();
            AtualizarComando();
        }

        private void AtualizarBadgeParams()
        {
            TxtBadgeParams.Text = string.IsNullOrWhiteSpace(_parametrosExtras)
                ? "Params"
                : $"Params ({_parametrosExtras})";
        }

        private void BtnCopiar_Click(object sender, RoutedEventArgs e)
        {
            var cmd = TxtComando.Text;
            if (string.IsNullOrWhiteSpace(cmd)) return;
            var pkg = new DataPackage();
            pkg.SetText(cmd);
            Clipboard.SetContent(pkg);
        }

        private async void BtnExecutar_Click(object sender, RoutedEventArgs e)
        {
            var pasta    = ListBinarios.SelectedItem as PastaBinariosDto;
            var sistema  = ListSistemas.SelectedItem as string;
            var servidor = (ListServidores.SelectedItem as ServidorStatusDto)?.Nome;
            var usuario  = TxtUsuario.Text?.Trim();
            var senha    = TxtSenha.Text;

            if (pasta == null || string.IsNullOrWhiteSpace(sistema) ||
                string.IsNullOrWhiteSpace(servidor)) return;

            var exeNome = _aplicativo switch
            {
                "Builder"    => "Builder.exe",
                "BuilderCli" => "BuilderCli.exe",
                "Instaler"   => "CS.exe",
                _            => "CS1.exe"
            };
            var exePath = Path.Combine(pasta.Caminho, exeNome);

            if (!File.Exists(exePath))
            {
                await new ContentDialog
                {
                    Title           = "Executável não encontrado",
                    Content         = $"Arquivo não encontrado:\n{exePath}",
                    CloseButtonText = "OK",
                    XamlRoot        = XamlRoot
                }.ShowAsync();
                return;
            }

            var args = _aplicativo switch
            {
                "Instaler" => $"{servidor} 5342 {sistema} -pu {usuario} -pp {senha}",
                "Runner"   => $"{sistema}@{servidor} -run -pu {usuario} -pp {senha}",
                _          => $"{sistema}@{servidor} -pu {usuario} -pp {senha}",
            };

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = exePath,
                    Arguments       = args,
                    UseShellExecute = true
                });

                _cfg.UltimaPasta      = pasta.Nome;
                _cfg.UltimoServidor   = servidor;
                _cfg.UltimoSistema    = sistema;
                _cfg.UltimoAplicativo = _aplicativo;

                if (!_cfg.Servidores.ContainsKey(servidor))
                    _cfg.Servidores[servidor] = new ServidorHistorico();
                _cfg.Servidores[servidor].UltimoSistema = sistema;

                await SelecionadorSistemaHelper.SalvarAsync(_cfg);
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title           = "Erro ao executar",
                    Content         = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot        = XamlRoot
                }.ShowAsync();
            }
        }

    }
}
