using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JrTools.Pages
{
    public sealed partial class InstalarArtefatosPage : Page
    {
        private const int MAX_LOG = 15000;
        private const string BASE_FONTES = @"D:\Benner\fontes\rh";
        private const string WES_BIN_SUBPATH    = @"WES\WebApp\Bin\wes.exe";
        private const string WES_CONFIG_SUBPATH = @"WES\WebApp\web.config";

        private ConfiguracaoRelatoriosRh _cfgRh;
        private bool _carregandoConfig = false;
        private bool _carregandoSistema = false;
        private string _diretorioBinarios = string.Empty;

        // Caminho derivado do projeto selecionado
        private string _wesExePath   = string.Empty;
        private string _webConfigPath = string.Empty;

        private System.Text.StringBuilder _logBuffer = new System.Text.StringBuilder();
        private DispatcherTimer _logTimer;

        private readonly ArtefatoService _artefatoService = new ArtefatoService();
        private System.Collections.Generic.List<ArtefatoDto> _todosArtefatos = new System.Collections.Generic.List<ArtefatoDto>();
        private string _webAppPath = string.Empty;

        public InstalarArtefatosPage()
        {
            InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += OnLoaded;
            
            _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _logTimer.Tick += (s, e) => FlushLogBuffer();
            _logTimer.Start();
        }

        private void FlushLogBuffer()
        {
            if (_logBuffer.Length == 0) return;
            string newText;
            lock (_logBuffer) { newText = _logBuffer.ToString(); _logBuffer.Clear(); }
            
            TxtLog.Text += newText;
            if (TxtLog.Text.Length > MAX_LOG) TxtLog.Text = TxtLog.Text[^MAX_LOG..];
            ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _carregandoConfig = true;

            _cfgRh = await ConfiguracaoRelatoriosHelper.LerAsync();
            var cfg = await ConfigHelper.LerConfiguracoesAsync();
            _diretorioBinarios = cfg?.DiretorioBinarios ?? string.Empty;

            TxtServidor.Text  = _cfgRh.Servidor;
            TxtUsuario.Text   = _cfgRh.Usuario;
            TxtSenha.Password = _cfgRh.Senha;

            if (!string.IsNullOrWhiteSpace(_cfgRh.Sistema))
            {
                CmbSistema.ItemsSource = new[] { _cfgRh.Sistema };
                CmbSistema.SelectedIndex = 0;
            }

            _carregandoConfig = false;

            await CarregarProjetosAsync();
        }

        // ── Projetos ─────────────────────────────────────────────────────────

        private async Task CarregarProjetosAsync()
        {
            var projetos = await Task.Run(() => Folders.ListarPastas(BASE_FONTES));
            CmbProjetoWes.ItemsSource       = projetos;
            CmbProjetoWes.DisplayMemberPath = "Nome";

            // Seleciona "prod" por padrão
            var prod = projetos.FirstOrDefault(p =>
                p.Nome.Equals("prod", StringComparison.OrdinalIgnoreCase));
            CmbProjetoWes.SelectedItem = prod ?? projetos.FirstOrDefault();
        }

        private void CmbProjetoWes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProjetoWes.SelectedItem is not PastaInformacoesDto projeto) return;

            _wesExePath    = Path.Combine(projeto.Caminho, WES_BIN_SUBPATH);
            _webConfigPath = Path.Combine(projeto.Caminho, WES_CONFIG_SUBPATH);
            _webAppPath    = Path.Combine(projeto.Caminho, @"WES\WebApp");
            TxtWesExePath.Text = _wesExePath;

            CarregarArtefatosDoProjeto();
        }

        // ── Configurações ────────────────────────────────────────────────────

        private async void TxtConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_carregandoConfig || _cfgRh == null) return;
            _cfgRh.Servidor = TxtServidor.Text;
            _cfgRh.Usuario  = TxtUsuario.Text;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        private async void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_carregandoConfig || _cfgRh == null) return;
            _cfgRh.Senha = TxtSenha.Password;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        // ── BServer ──────────────────────────────────────────────────────────

        private async void BtnCarregarSistemas_Click(object sender, RoutedEventArgs e)
        {
            if (_carregandoSistema) return;
            _carregandoSistema = true;
            LoadingSistemas.IsActive = true;
            BtnCarregarSistemas.IsEnabled = false;

            var sistemaAtual = CmbSistema.SelectedItem as string ?? _cfgRh?.Sistema;
            AppendLog($"[BSERVER] Conectando em {TxtServidor.Text}...");

            var resultado = await BServerQueryService.ConsultarAsync(TxtServidor.Text, _diretorioBinarios);

            if (resultado.IsSuccess)
            {
                CmbSistema.ItemsSource = resultado.AvailableSystems;
                var idx = Array.FindIndex(resultado.AvailableSystems,
                    s => string.Equals(s, sistemaAtual, StringComparison.OrdinalIgnoreCase));
                CmbSistema.SelectedIndex = idx >= 0 ? idx : (resultado.AvailableSystems.Length > 0 ? 0 : -1);
                AppendLog($"[BSERVER] {resultado.AvailableSystems.Length} sistema(s) encontrado(s). {resultado.ErrorMessage}");
            }
            else
            {
                AppendLog($"[BSERVER ERRO] {resultado.ErrorMessage}");
            }

            LoadingSistemas.IsActive = false;
            BtnCarregarSistemas.IsEnabled = true;
            _carregandoSistema = false;
        }

        private async void CmbSistema_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_carregandoConfig || _carregandoSistema || _cfgRh == null) return;
            if (CmbSistema.SelectedItem is string sistema)
            {
                _cfgRh.Sistema = sistema;
                await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
            }
        }

        // ── Comandos WES ─────────────────────────────────────────────────────

        private async void BtnConfigSet_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos()) return;
            await ExecutarComando(LoadingConfigSet, BtnConfigSet, async wes =>
            {
                await wes.ConfigSetAsync(TxtServidor.Text, CmbSistema.SelectedItem as string ?? string.Empty, TxtUsuario.Text, TxtSenha.Password, CriarProgresso());
                InjetarUseCOMFree();
            });
        }

        private async void BtnCacheClear_Click(object sender, RoutedEventArgs e)
            => await ExecutarComando(LoadingCacheClear, BtnCacheClear,
                wes => wes.CacheClearAsync(CriarProgresso()));

        private async void BtnArtifactsInstall_Click(object sender, RoutedEventArgs e)
            => await ExecutarComando(LoadingArtifacts, BtnArtifactsInstall, async wes =>
            {
                var layers = new System.Collections.Generic.List<string>();
                if (ChkLayerBuilder.IsChecked == true) layers.Add("builder");
                if (ChkLayerTecnologia.IsChecked == true) layers.Add("tecnologia");
                if (ChkLayerBenner.IsChecked == true) layers.Add("benner");
                if (ChkLayerVertical.IsChecked == true) layers.Add("vertical");
                if (ChkLayerEspecifico.IsChecked == true) layers.Add("especifico");
                if (ChkLayerCliente.IsChecked == true) layers.Add("cliente");
                foreach (var layer in layers)
                {
                    int maxRetries = 3;
                    for (int i = 1; i <= maxRetries; i++)
                    {
                        int exitCode = await wes.ArtifactsInstallLayerAsync(layer, CriarProgresso());
                        if (exitCode == 0) break; // Sucesso, avança para a próxima camada
                        
                        if (i < maxRetries)
                        {
                            AppendLog($"[AVISO] Camada '{layer}' falhou (ExitCode: {exitCode}). Retentando em 3 segundos ({i}/{maxRetries})...");
                            await Task.Delay(3000);
                        }
                        else
                        {
                            throw new Exception($"Falha ao instalar a camada '{layer}' após {maxRetries} tentativas.");
                        }
                    }
                }
            });

        private async void BtnPagesGenerate_Click(object sender, RoutedEventArgs e)
            => await ExecutarComando(LoadingPages, BtnPagesGenerate,
                wes => wes.PagesGenerateAsync(CriarProgresso()));

        // ── web.config ───────────────────────────────────────────────────────

        private void InjetarUseCOMFree()
        {
            const string KEY = "useCOMFree";

            if (string.IsNullOrWhiteSpace(_webConfigPath) || !File.Exists(_webConfigPath))
            {
                AppendLog($"[WEB.CONFIG] Arquivo não encontrado: {_webConfigPath}");
                return;
            }

            try
            {
                var doc = XDocument.Load(_webConfigPath);
                var appSettings = doc.Root?.Element("appSettings");

                if (appSettings == null)
                {
                    // Cria o nó se não existir
                    appSettings = new XElement("appSettings");
                    doc.Root!.Add(appSettings);
                }

                // Verifica se a chave já existe
                var existente = appSettings.Elements("add")
                    .FirstOrDefault(el => el.Attribute("key")?.Value == KEY);

                if (existente != null)
                {
                    AppendLog($"[WEB.CONFIG] Chave '{KEY}' já existe, ignorando.");
                    return;
                }

                appSettings.Add(new XElement("add",
                    new XAttribute("key", KEY),
                    new XAttribute("value", "false")));

                doc.Save(_webConfigPath);
                AppendLog($"[WEB.CONFIG] Chave '{KEY}' adicionada em: {_webConfigPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"[WEB.CONFIG ERRO] {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private WesService CriarWesService()
        {
            if (string.IsNullOrWhiteSpace(_wesExePath))
                throw new InvalidOperationException("Selecione um projeto WES antes de executar.");
            return new WesService(_wesExePath);
        }

        private bool ValidarCampos()
        {
            if (string.IsNullOrWhiteSpace(TxtServidor.Text) ||
                CmbSistema.SelectedItem is not string s || string.IsNullOrWhiteSpace(s) ||
                string.IsNullOrWhiteSpace(TxtUsuario.Text)  ||
                string.IsNullOrWhiteSpace(TxtSenha.Password))
            {
                InfoBarAviso.Message  = "Preencha todos os campos de configuração antes de executar.";
                InfoBarAviso.Severity = InfoBarSeverity.Error;
                InfoBarAviso.IsOpen   = true;
                return false;
            }
            return true;
        }

        private async Task ExecutarComando(ProgressRing loading, Button btn, Func<WesService, Task> acao)
        {
            InfoBarAviso.IsOpen = false;
            loading.IsActive    = true;
            btn.IsEnabled       = false;
            try
            {
                await acao(CriarWesService());
            }
            catch (Exception ex)
            {
                InfoBarAviso.Message  = ex.Message;
                InfoBarAviso.Severity = InfoBarSeverity.Error;
                InfoBarAviso.IsOpen   = true;
                AppendLog($"[ERRO] {ex.Message}");
            }
            finally
            {
                loading.IsActive = false;
                btn.IsEnabled    = true;
            }
        }

        private IProgress<string> CriarProgresso() => new Progress<string>(AppendLog);

        private void AppendLog(string linha)
        {
            lock (_logBuffer) { _logBuffer.AppendLine(linha); }
        }

        private void BtnLimparLog_Click(object sender, RoutedEventArgs e)
        {
            lock (_logBuffer) { _logBuffer.Clear(); }
            TxtLog.Text = string.Empty;
        }

        // ── Inspetor de Artefatos e Auto-Fix ───────────────────────────────

        private void CarregarArtefatosDoProjeto()
        {
            if (string.IsNullOrWhiteSpace(_webAppPath)) return;

            // Popula ComboBox de Status se não populado
            if (CmbStatusArtefato.ItemsSource == null)
            {
                CmbStatusArtefato.ItemsSource = new System.Collections.Generic.List<string>
                {
                    "Todos os Artefatos",
                    "Somente Pendentes (Novos/Modificados)",
                    "Apenas Novos (FileOnly)",
                    "Apenas Modificados (Diferent)",
                    "Apenas Instalados (Equal)"
                };
                CmbStatusArtefato.SelectedIndex = 0;
            }

            _todosArtefatos = _artefatoService.CarregarArtefatos(_webAppPath);

            // Popula ComboBox de Guias
            var guias = new System.Collections.Generic.List<string> { "Todas" };
            guias.AddRange(_todosArtefatos.Select(a => a.Guia).Distinct().OrderBy(g => g));
            CmbGuiaArtefato.ItemsSource = guias;
            CmbGuiaArtefato.SelectedIndex = 0;

            // Popula ComboBox de Camadas
            var camadas = new System.Collections.Generic.List<string> { "Todas" };
            camadas.AddRange(_todosArtefatos.Select(a => a.Camada).Distinct().OrderBy(c => c));
            CmbCamadaArtefato.ItemsSource = camadas;
            CmbCamadaArtefato.SelectedIndex = 0;

            FiltrarArtefatos();
        }

        private async Task CompararComBancoDadosAsync()
        {
            if (string.IsNullOrWhiteSpace(_webAppPath) || _todosArtefatos == null || _todosArtefatos.Count == 0) return;

            try
            {
                BtnCompararDB.IsEnabled = false;
                InfoBarAviso.IsOpen = true;
                InfoBarAviso.Severity = InfoBarSeverity.Informational;
                InfoBarAviso.Title = "Comparando artefatos...";
                InfoBarAviso.Message = "Consultando status no Banco de Dados via BennerSmartInstaller...";

                var wes = new WesService(_wesExePath);
                var statusDict = await wes.ArtifactsCompareAsync(_webAppPath, CriarProgresso());

                if (statusDict != null && statusDict.Count > 0)
                {
                    int pendentesCount = 0;
                    foreach (var art in _todosArtefatos)
                    {
                        if (statusDict.TryGetValue(art.NomeArquivo, out string st) ||
                            statusDict.TryGetValue(art.Identificador, out st))
                        {
                            art.Status = st;
                        }
                        else
                        {
                            art.Status = "Equal";
                        }

                        if (art.IsPendente) pendentesCount++;
                    }

                    InfoBarAviso.Severity = InfoBarSeverity.Success;
                    InfoBarAviso.Title = "Comparação concluída";
                    InfoBarAviso.Message = $"Foram encontrados {pendentesCount} artefato(s) pendente(s) de instalação.";
                }
                else
                {
                    InfoBarAviso.Severity = InfoBarSeverity.Warning;
                    InfoBarAviso.Title = "Comparação concluída sem dados";
                    InfoBarAviso.Message = "Não foi possível obter a comparação do banco de dados ou todos estão atualizados.";
                }
            }
            catch (Exception ex)
            {
                InfoBarAviso.Severity = InfoBarSeverity.Error;
                InfoBarAviso.Title = "Erro na comparação";
                InfoBarAviso.Message = ex.Message;
            }
            finally
            {
                BtnCompararDB.IsEnabled = true;
                FiltrarArtefatos();
            }
        }

        private void FiltrarArtefatos()
        {
            if (_todosArtefatos == null) return;

            string statusSel = CmbStatusArtefato.SelectedItem as string ?? "Todos os Artefatos";
            string guiaSel = CmbGuiaArtefato.SelectedItem as string ?? "Todas";
            string camadaSel = CmbCamadaArtefato.SelectedItem as string ?? "Todas";
            string busca = TxtBuscaArtefato.Text?.Trim() ?? string.Empty;

            var filtrados = _todosArtefatos.AsEnumerable();

            if (statusSel.StartsWith("Somente Pendentes"))
                filtrados = filtrados.Where(a => a.IsPendente);
            else if (statusSel.StartsWith("Apenas Novos"))
                filtrados = filtrados.Where(a => a.Status.Equals("FileOnly", StringComparison.OrdinalIgnoreCase) || a.Status.Equals("Novo", StringComparison.OrdinalIgnoreCase));
            else if (statusSel.StartsWith("Apenas Modificados"))
                filtrados = filtrados.Where(a => a.Status.Equals("Diferent", StringComparison.OrdinalIgnoreCase) || a.Status.Equals("Modificado", StringComparison.OrdinalIgnoreCase));
            else if (statusSel.StartsWith("Apenas Instalados"))
                filtrados = filtrados.Where(a => a.Status.Equals("Equal", StringComparison.OrdinalIgnoreCase));

            if (guiaSel != "Todas")
                filtrados = filtrados.Where(a => a.Guia.Equals(guiaSel, StringComparison.OrdinalIgnoreCase));

            if (camadaSel != "Todas")
                filtrados = filtrados.Where(a => a.Camada.Equals(camadaSel, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(busca))
                filtrados = filtrados.Where(a => a.Identificador.Contains(busca, StringComparison.OrdinalIgnoreCase) ||
                                                 a.NomeArquivo.Contains(busca, StringComparison.OrdinalIgnoreCase));

            LstArtefatos.ItemsSource = filtrados.ToList();
        }

        private void CmbFiltroArtefato_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => FiltrarArtefatos();

        private void TxtBuscaArtefato_TextChanged(object sender, TextChangedEventArgs e)
            => FiltrarArtefatos();

        private void BtnCarregarArtefatos_Click(object sender, RoutedEventArgs e)
            => CarregarArtefatosDoProjeto();

        private async void BtnCompararDB_Click(object sender, RoutedEventArgs e)
            => await CompararComBancoDadosAsync();

        private void BtnMarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            if (LstArtefatos.ItemsSource is System.Collections.Generic.IEnumerable<ArtefatoDto> visiveis)
            {
                var listaVisiveis = visiveis.ToList();
                foreach (var item in listaVisiveis)
                {
                    item.IsSelecionado = true;
                }
                LstArtefatos.ItemsSource = listaVisiveis;
            }
        }

        private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
        {
            if (_todosArtefatos != null)
            {
                foreach (var item in _todosArtefatos)
                {
                    item.IsSelecionado = false;
                }
                if (LstArtefatos.ItemsSource is System.Collections.Generic.IEnumerable<ArtefatoDto> visiveis)
                {
                    LstArtefatos.ItemsSource = visiveis.ToList();
                }
            }
        }

        private void LstArtefatos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstArtefatos.SelectedItem is ArtefatoDto artefato)
            {
                TxtInfoDependencia.Text = $"Selecionado na lista: {artefato.Identificador} ({artefato.Guia} - Camada {artefato.Camada})";
            }
        }

        private void BtnInspecionarDep_Click(object sender, RoutedEventArgs e)
        {
            var selecionados = _todosArtefatos.Where(a => a.IsSelecionado).ToList();
            if (selecionados.Count == 0 && LstArtefatos.SelectedItem is ArtefatoDto itemUnico)
            {
                selecionados.Add(itemUnico);
            }

            if (selecionados.Count == 0)
            {
                TxtInfoDependencia.Text = "Aviso: Marque com a Checkbox ou selecione um artefato na lista acima para inspecionar dependências.";
                return;
            }

            var dependencias = _artefatoService.ResolverDependencias(selecionados, _todosArtefatos);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔎 Ordem de Instalação Calculada para {selecionados.Count} artefato(s) selecionado(s) (Total com dependências: {dependencias.Count}):");
            for (int i = 0; i < dependencias.Count; i++)
            {
                var item = dependencias[i];
                sb.AppendLine($"  {i + 1}. {item.Guia} -> {item.Identificador} (Camada {item.Camada})");
            }
            TxtInfoDependencia.Text = sb.ToString();
        }

        private async void BtnInstalarSmart_Click(object sender, RoutedEventArgs e)
        {
            var selecionados = _todosArtefatos.Where(a => a.IsSelecionado).ToList();
            if (selecionados.Count == 0 && LstArtefatos.SelectedItem is ArtefatoDto itemUnico)
            {
                selecionados.Add(itemUnico);
            }

            if (selecionados.Count == 0)
            {
                InfoBarAviso.Message = "Marque ao menos um artefato utilizando a Checkbox (ou selecione na lista) antes de executar o Smart Install.";
                InfoBarAviso.Severity = InfoBarSeverity.Warning;
                InfoBarAviso.IsOpen = true;
                return;
            }

            var dependencias = _artefatoService.ResolverDependencias(selecionados, _todosArtefatos);

            AppendLog($"[SMART INSTALL] Iniciando instalação para {selecionados.Count} artefato(s) selecionado(s) e {dependencias.Count - selecionados.Count} dependência(s) encontrada(s).");
            foreach (var dep in dependencias)
            {
                AppendLog($"  -> Requer: {dep.Guia} / {dep.Identificador} (Camada {dep.Camada})");
            }

            var arquivosRelativos = dependencias.Select(d => Path.Combine("Artifacts", d.Guia, d.NomeArquivo)).ToList();

            AppendLog($"[SMART INSTALL SELETIVO] Executando instalador nativo cirúrgico para {arquivosRelativos.Count} arquivo(s)...");

            await ExecutarComando(LoadingArtifacts, BtnInstalarSmart, async wes =>
            {
                await wes.ArtifactsInstallSelectiveAsync(_webAppPath, arquivosRelativos, CriarProgresso());
            });
        }
    }
}
