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

        public InstalarArtefatosPage()
        {
            InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += OnLoaded;
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

            CarregarProjetos();
        }

        // ── Projetos ─────────────────────────────────────────────────────────

        private void CarregarProjetos()
        {
            var projetos = Folders.ListarPastas(BASE_FONTES);
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
            TxtWesExePath.Text = _wesExePath;
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
            => await ExecutarComando(LoadingArtifacts, BtnArtifactsInstall,
                wes => wes.ArtifactsInstallAsync(CriarProgresso()));

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
            if (DispatcherQueue.HasThreadAccess) EscreverLog(linha);
            else DispatcherQueue.TryEnqueue(() => EscreverLog(linha));
        }

        private void EscreverLog(string linha)
        {
            TxtLog.Text += linha + "\n";
            if (TxtLog.Text.Length > MAX_LOG)
                TxtLog.Text = TxtLog.Text[^MAX_LOG..];
            ScrollLog.ChangeView(null, ScrollLog.ScrollableHeight, null);
        }

        private void BtnLimparLog_Click(object sender, RoutedEventArgs e)
            => TxtLog.Text = string.Empty;
    }
}
