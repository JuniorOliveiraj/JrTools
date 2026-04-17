using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Pages.Apps
{
    public sealed partial class SubirAmbienteManualPage : Page
    {
        private const int MAX_LOG = 15000;
        private const string PASTA_BINARIOS_TEMP = @"D:\Benner\bin\delphi";
        private const string BASE_FONTES         = @"D:\Benner\fontes\rh";
        private const string WES_SUBPATH         = @"WES\WebApp";
        private const string WES_BIN_SUBPATH     = @"WES\WebApp\Bin\wes.exe";
        private const string WES_CONFIG_SUBPATH  = @"WES\WebApp\web.config";

        private ConfiguracaoRelatoriosRh _cfgRh;
        private ConfiguracoesdataObject  _cfg;
        private string _wesExePath   = string.Empty;
        private string _webConfigPath = string.Empty;
        private bool _carregandoConfig = false;

        private readonly IisService _iis = new();

        public SubirAmbienteManualPage()
        {
            InitializeComponent();
            NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _carregandoConfig = true;

            _cfgRh = await ConfiguracaoRelatoriosHelper.LerAsync();
            _cfg   = await ConfigHelper.LerConfiguracoesAsync();
            _wesExePath = _cfg?.WesExePath ?? @"D:\Benner\fontes\rh\prod\WES\WebApp\Bin\wes.exe";

            TxtServidor.Text  = _cfgRh.Servidor;
            TxtSistema.Text   = _cfgRh.Sistema;
            TxtUsuario.Text   = _cfgRh.Usuario;
            TxtSenha.Password = _cfgRh.Senha;

            if (!string.IsNullOrWhiteSpace(_cfg?.UltimaPastaAmbiente))
                AutoPasta.Text = _cfg.UltimaPastaAmbiente;

            _carregandoConfig = false;

            CarregarBranches();
            await Task.WhenAll(CarregarProjetosWesAsync(), CarregarProjetosIisAsync(), CarregarPoolsAsync());
        }

        // ── Projeto WES ──────────────────────────────────────────────────────

        private Task CarregarProjetosWesAsync()
            => Task.Run(() =>
            {
                var projetos = Folders.ListarPastas(BASE_FONTES);
                DispatcherQueue.TryEnqueue(() =>
                {
                    CmbProjetoWes.ItemsSource       = projetos;
                    CmbProjetoWes.DisplayMemberPath = "Nome";
                    CmbProjetoWes.PlaceholderText   = "Selecione o projeto";

                    var prod = projetos.FirstOrDefault(p =>
                        p.Nome.Equals("prod", StringComparison.OrdinalIgnoreCase));
                    CmbProjetoWes.SelectedItem = prod ?? projetos.FirstOrDefault();
                });
            });

        private void CmbProjetoWes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProjetoWes.SelectedItem is not PastaInformacoesDto projeto) return;
            _wesExePath    = Path.Combine(projeto.Caminho, WES_BIN_SUBPATH);
            _webConfigPath = Path.Combine(projeto.Caminho, WES_CONFIG_SUBPATH);
            TxtWesExePath.Text = _wesExePath;
        }

        // ── Configurações WES ────────────────────────────────────────────────

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

        private async void BtnSetarConfiguracoes_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarCamposWes()) return;

            InfoBarAviso.IsOpen = false;
            LoadingConfigSet.IsActive = true;
            BtnSetarConfiguracoes.IsEnabled = false;
            try
            {
                var wes = new WesService(_wesExePath);
                await wes.ConfigSetAsync(TxtServidor.Text, TxtSistema.Text, TxtUsuario.Text, TxtSenha.Password, CriarProgresso());
                await Task.Run(() => InjetarUseCOMFree());
            }
            catch (Exception ex) { MostrarErro(ex.Message); }
            finally
            {
                LoadingConfigSet.IsActive = false;
                BtnSetarConfiguracoes.IsEnabled = true;
            }
        }

        private async void BtnLimparCache_Click(object sender, RoutedEventArgs e)
            => await ExecutarWes(LoadingLimparCache, BtnLimparCache,
                wes => wes.CacheClearAsync(CriarProgresso()));

        private async void BtnInstalarArtefatos_Click(object sender, RoutedEventArgs e)
            => await ExecutarWes(LoadingInstalarArtefatos, BtnInstalarArtefatos,
                wes => wes.ArtifactsInstallAsync(CriarProgresso()));

        private async void BtnRegenerarPaginas_Click(object sender, RoutedEventArgs e)
            => await ExecutarWes(LoadingRegenerarPaginas, BtnRegenerarPaginas,
                wes => wes.PagesGenerateAsync(CriarProgresso()));

        private async Task ExecutarWes(ProgressRing loading, Button btn, Func<WesService, Task> acao)
        {
            if (string.IsNullOrWhiteSpace(_wesExePath))
            {
                MostrarErro("Selecione um projeto WES antes de executar.");
                return;
            }
            InfoBarAviso.IsOpen = false;
            loading.IsActive    = true;
            btn.IsEnabled       = false;
            try   { await acao(new WesService(_wesExePath)); }
            catch (Exception ex) { MostrarErro(ex.Message); AppendLog($"[ERRO] {ex.Message}"); }
            finally { loading.IsActive = false; btn.IsEnabled = true; }
        }

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
                var doc         = System.Xml.Linq.XDocument.Load(_webConfigPath);
                var appSettings = doc.Root?.Element("appSettings");
                if (appSettings == null)
                {
                    appSettings = new System.Xml.Linq.XElement("appSettings");
                    doc.Root!.Add(appSettings);
                }
                var existente = appSettings.Elements("add")
                    .FirstOrDefault(el => el.Attribute("key")?.Value == KEY);
                if (existente != null) { AppendLog($"[WEB.CONFIG] Chave '{KEY}' já existe."); return; }

                appSettings.Add(new System.Xml.Linq.XElement("add",
                    new System.Xml.Linq.XAttribute("key", KEY),
                    new System.Xml.Linq.XAttribute("value", "false")));
                doc.Save(_webConfigPath);
                AppendLog($"[WEB.CONFIG] Chave '{KEY}' adicionada com sucesso.");
            }
            catch (Exception ex) { AppendLog($"[WEB.CONFIG ERRO] {ex.Message}"); }
        }

        // ── AutoSuggestBox ───────────────────────────────────────────────────

        private void AutoPasta_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var historico = _cfg?.HistoricoPastasAmbiente ?? new List<string>();
            var filtro    = sender.Text.Trim();
            sender.ItemsSource = string.IsNullOrWhiteSpace(filtro)
                ? historico
                : historico.Where(p => p.Contains(filtro, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void AutoPasta_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
            => sender.Text = args.SelectedItem?.ToString() ?? sender.Text;

        private async void BtnAdicionarLink_Click(object sender, RoutedEventArgs e)
        {
            var nomePasta = AutoPasta.Text.Trim();
            if (string.IsNullOrWhiteSpace(nomePasta)) { MostrarErro("Informe o nome da pasta."); return; }

            await SalvarPastaNoHistoricoAsync(nomePasta);
            InfoBarAviso.IsOpen        = false;
            LoadingLink.IsActive       = true;
            BtnAdicionarLink.IsEnabled = false;
            try   { await Task.Run(() => CriarLink(nomePasta)); }
            catch (Exception ex) { MostrarErro(ex.Message); AppendLog($"[ERRO] {ex.Message}"); }
            finally { LoadingLink.IsActive = false; BtnAdicionarLink.IsEnabled = true; }
        }

        // ── Atualizar Binários ───────────────────────────────────────────────

        private void CarregarBranches()
        {
            CmbBranch.ItemsSource = new List<string>
            {
                "prd/09.00", "prd/08.06", "prd/08.05", "prd/08.04",
                "dev/09.00.00", "dev/08.06.00", "dev/08.05.00", "dev/08.04.00"
            };
            CmbBranch.SelectedIndex = 0;
        }

        private async void BtnAtualizarBinarios_Click(object sender, RoutedEventArgs e)
        {
            var branch     = CmbBranch.SelectedItem?.ToString();
            var reutilizar = ToggleReutilizarBinarios.IsOn;
            var usarLink   = ToggleUsarLink.IsOn;
            var nomePasta  = AutoPasta.Text.Trim();

            if (string.IsNullOrWhiteSpace(branch))
            {
                MostrarErro("Selecione uma branch.");
                return;
            }
            if (usarLink && string.IsNullOrWhiteSpace(nomePasta))
            {
                MostrarErro("Informe o nome da pasta para o link.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(nomePasta))
                await SalvarPastaNoHistoricoAsync(nomePasta);

            InfoBarAviso.IsOpen            = false;
            LoadingBinarios.IsActive       = true;
            BtnAtualizarBinarios.IsEnabled = false;
            try
            {
                await BaixarEExtrairBinariosAsync(branch, reutilizar, usarLink, nomePasta);
            }
            catch (Exception ex)
            {
                MostrarErro(ex.Message);
                AppendLog($"[ERRO] {ex.Message}");
            }
            finally
            {
                LoadingBinarios.IsActive       = false;
                BtnAtualizarBinarios.IsEnabled = true;
            }
        }

        private async Task BaixarEExtrairBinariosAsync(string branch, bool reutilizar, bool usarLink, string nomePasta)
        {
            var progresso = CriarProgresso();
            var svc       = new BinarioService();

            var branchNorm = new JrTools.Utils.BranchNameHelper()
                .ObterBranchInfo(branch).Branch
                .Replace("/", "-");

            progresso.Report($"[INFO] Buscando binário para '{branchNorm}'...");
            var binInfo = await svc.ObterBinarioAsync(branchNorm);
            if (binInfo == null)
                throw new InvalidOperationException($"Binário não encontrado para '{branchNorm}' no servidor.");

            // Se não reutilizar, apaga o zip da pasta temp para forçar novo download
            if (!reutilizar)
            {
                var pastaTemp = Path.Combine(Path.GetTempPath(), "BinariosTemp");
                var zipExist  = Path.Combine(pastaTemp, binInfo.NomeOriginal + ".zip");
                if (File.Exists(zipExist))
                {
                    progresso.Report($"[INFO] Removendo zip existente: {zipExist}");
                    File.Delete(zipExist);
                }
            }

            binInfo.destino = @"D:\Benner\bin";

            if (usarLink)
            {
                // Com link: extrai apenas para Delphi, o link faz o papel da pasta nomeada
                await svc.ExtrairApenasDelphiAsync(binInfo, progresso);

                if (!string.IsNullOrWhiteSpace(nomePasta))
                {
                    progresso.Report($"[INFO] Criando link simbólico '{nomePasta}' → Delphi...");
                    CriarLink(nomePasta);
                }
            }
            else
            {
                // Sem link: extrai para Delphi e para RH_LOCAL_DESENV_XX normalmente
                await svc.ExtrairBinarioAsync(binInfo, progresso);
            }
        }

        private void CriarLink(string nomePasta)
        {
            var destino = $@"D:\Benner\bin\{nomePasta}";

            if (Directory.Exists(destino))
            {
                AppendLog($"[AVISO] Destino já existe: {destino}. Removendo antes de criar o link...");
                Directory.Delete(destino, recursive: false);
            }

            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = $"/c mklink /J \"{destino}\" \"{PASTA_BINARIOS_TEMP}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            var error  = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output)) AppendLog($"[LINK] {output.Trim()}");
            if (!string.IsNullOrWhiteSpace(error))  AppendLog($"[LINK ERRO] {error.Trim()}");

            AppendLog(proc.ExitCode == 0
                ? $"[INFO] Link criado com sucesso: {destino}"
                : $"[ERRO] Falha ao criar link (código {proc.ExitCode}).");
        }

        private async Task SalvarPastaNoHistoricoAsync(string nomePasta)
        {
            if (_cfg == null) return;
            _cfg.UltimaPastaAmbiente = nomePasta;
            if (!_cfg.HistoricoPastasAmbiente.Contains(nomePasta, StringComparer.OrdinalIgnoreCase))
                _cfg.HistoricoPastasAmbiente.Insert(0, nomePasta);
            await ConfigHelper.SalvarConfiguracoesAsync(_cfg);
        }

        // ── IIS ──────────────────────────────────────────────────────────────

        private Task CarregarProjetosIisAsync()
            => Task.Run(() =>
            {
                var projetos = Folders.ListarPastas(BASE_FONTES);
                DispatcherQueue.TryEnqueue(() =>
                {
                    CmbProjeto.ItemsSource       = projetos;
                    CmbProjeto.DisplayMemberPath = "Nome";
                    CmbProjeto.PlaceholderText   = projetos.Count == 0 ? "Nenhum projeto encontrado" : "Selecione o projeto";
                });
            });

        private async Task CarregarPoolsAsync()
        {
            try
            {
                var pools = await _iis.ListarPoolsAsync();
                CmbPool.ItemsSource    = pools;
                CmbPool.PlaceholderText = pools.Count == 0 ? "Nenhuma pool encontrada" : "Selecione a pool";
            }
            catch (Exception ex)
            {
                AppendLog($"[AVISO] Não foi possível listar pools: {ex.Message}");
                CmbPool.PlaceholderText = "Erro ao carregar pools";
            }
        }

        private void CmbProjeto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProjeto.SelectedItem is not PastaInformacoesDto projeto) return;
            TxtCaminhoIis.Text = Path.Combine(projeto.Caminho, WES_SUBPATH);
        }

        private async void BtnCriarApp_Click(object sender, RoutedEventArgs e)
        {
            var site    = TxtSiteIis.Text.Trim();
            var nomeApp = TxtNomeApp.Text.Trim();
            var pool    = CmbPool.SelectedItem?.ToString();
            var caminho = TxtCaminhoIis.Text.Trim();

            if (string.IsNullOrWhiteSpace(site)    ||
                string.IsNullOrWhiteSpace(nomeApp) ||
                string.IsNullOrWhiteSpace(pool)    ||
                string.IsNullOrWhiteSpace(caminho))
            {
                MostrarErro("Preencha todos os campos do card IIS antes de criar.");
                return;
            }

            InfoBarAviso.IsOpen      = false;
            LoadingCriarApp.IsActive = true;
            BtnCriarApp.IsEnabled    = false;
            try
            {
                await _iis.CriarAplicacaoAsync(site, nomeApp, pool, caminho, CriarProgresso());
            }
            catch (Exception ex)
            {
                MostrarErro(ex.Message);
                AppendLog($"[ERRO] {ex.Message}");
            }
            finally
            {
                LoadingCriarApp.IsActive = false;
                BtnCriarApp.IsEnabled    = true;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool ValidarCamposWes()
        {
            if (string.IsNullOrWhiteSpace(TxtServidor.Text) ||
                string.IsNullOrWhiteSpace(TxtSistema.Text)  ||
                string.IsNullOrWhiteSpace(TxtUsuario.Text)  ||
                string.IsNullOrWhiteSpace(TxtSenha.Password))
            {
                MostrarErro("Preencha todos os campos de configuração antes de executar.");
                return false;
            }
            return true;
        }

        private void MostrarErro(string msg)
        {
            InfoBarAviso.Message  = msg;
            InfoBarAviso.Severity = InfoBarSeverity.Error;
            InfoBarAviso.IsOpen   = true;
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
