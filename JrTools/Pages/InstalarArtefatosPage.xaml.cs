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

            TxtServidor.Text  = _cfgRh.Servidor;
            TxtSistema.Text   = _cfgRh.Sistema;
            TxtUsuario.Text   = _cfgRh.Usuario;
            TxtSenha.Password = _cfgRh.Senha;

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
            _cfgRh.Sistema  = TxtSistema.Text;
            _cfgRh.Usuario  = TxtUsuario.Text;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        private async void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_carregandoConfig || _cfgRh == null) return;
            _cfgRh.Senha = TxtSenha.Password;
            await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
        }

        // ── Comandos WES ─────────────────────────────────────────────────────

        private async void BtnConfigSet_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCampos()) return;
            await ExecutarComando(LoadingConfigSet, BtnConfigSet, async wes =>
            {
                await wes.ConfigSetAsync(TxtServidor.Text, TxtSistema.Text, TxtUsuario.Text, TxtSenha.Password, CriarProgresso());
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
                string.IsNullOrWhiteSpace(TxtSistema.Text)  ||
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

        private void FiltrarArtefatos()
        {
            if (_todosArtefatos == null) return;

            string guiaSel = CmbGuiaArtefato.SelectedItem as string ?? "Todas";
            string camadaSel = CmbCamadaArtefato.SelectedItem as string ?? "Todas";
            string busca = TxtBuscaArtefato.Text?.Trim() ?? string.Empty;

            var filtrados = _todosArtefatos.AsEnumerable();

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

        private void LstArtefatos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstArtefatos.SelectedItem is ArtefatoDto artefato)
            {
                TxtInfoDependencia.Text = $"Selecionado: {artefato.Identificador} ({artefato.Guia} - Camada {artefato.Camada})";
            }
        }

        private void BtnInspecionarDep_Click(object sender, RoutedEventArgs e)
        {
            if (LstArtefatos.SelectedItem is not ArtefatoDto selecionado)
            {
                TxtInfoDependencia.Text = "Aviso: Selecione um artefato na lista acima para inspecionar dependências.";
                return;
            }

            var dependencias = _artefatoService.ResolverDependencias(selecionado, _todosArtefatos);

            if (dependencias.Count <= 1)
            {
                TxtInfoDependencia.Text = $"Nenhuma dependência extra encontrada no XML de [{selecionado.Identificador}]. Ele pode ser instalado diretamente.";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔎 Ordem de Instalação Calculada para [{selecionado.Identificador}]:");
                for (int i = 0; i < dependencias.Count; i++)
                {
                    var item = dependencias[i];
                    sb.AppendLine($"  {i + 1}. {item.Guia} -> {item.Identificador} (Camada {item.Camada})");
                }
                TxtInfoDependencia.Text = sb.ToString();
            }
        }

        private async void BtnInstalarSmart_Click(object sender, RoutedEventArgs e)
        {
            if (LstArtefatos.SelectedItem is not ArtefatoDto selecionado)
            {
                InfoBarAviso.Message = "Selecione um artefato na lista antes de executar o Smart Install.";
                InfoBarAviso.Severity = InfoBarSeverity.Warning;
                InfoBarAviso.IsOpen = true;
                return;
            }

            var dependencias = _artefatoService.ResolverDependencias(selecionado, _todosArtefatos);

            // Coleta todas as camadas únicas necessárias para instalar o artefato + suas dependências
            var camadasNecessarias = dependencias.Select(d => d.Camada).Distinct().ToList();

            AppendLog($"[SMART INSTALL] Iniciando instalação para [{selecionado.Identificador}] e {dependencias.Count - 1} dependências encontradas.");
            foreach(var dep in dependencias)
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
