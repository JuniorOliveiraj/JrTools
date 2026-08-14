using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class ConfiguracoesPage : Page
    {
        private ConfiguracoesdataObject _config;
        private DadosPessoaisDataObject _dadosPessoais;

        private bool _senhaVisible = false;
        private bool _geminiVisible = false;
        private bool _togglVisible = false;
        private bool _senhaSisconVisible = false;

        private bool _bserverSistemaChanging = false;
        private bool _fonteBinariosChanging = false;
        private bool _jenkinsTokenVisible = false;

        public ConfiguracoesPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += ConfiguracoesPage_Loaded;
        }

        private async void ConfiguracoesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarConfiguracoes();
            await CarregarDadosPessoais();
            CarregarMsBuildVersions();
            await CarregarIisPools();
        }

        #region Parametrizações

        private async System.Threading.Tasks.Task CarregarIisPools()
        {
            try
            {
                var iisService = new IisService();
                var pools = await iisService.ListarPoolsAsync();
                IisPoolComboBox.ItemsSource = pools;

                if (!string.IsNullOrEmpty(_config?.PoolIisPadrao))
                {
                    IisPoolComboBox.SelectedItem = pools.FirstOrDefault(p => p == _config.PoolIisPadrao);
                }
            }
            catch (Exception ex)
            {
                // Silencioso se der erro (IIS não instalado, etc)
            }
        }

        private async void IisPoolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config != null && IisPoolComboBox.SelectedItem is string selectedPool)
            {
                _config.PoolIisPadrao = selectedPool;
                await ConfigHelper.SalvarConfiguracoesAsync(_config);
            }
        }

        private void CarregarMsBuildVersions()
        {
            var msBuildVersions = MsBuildLocator.FindMsBuildVersions();
            MsBuildVersionComboBox.ItemsSource = msBuildVersions;

            if (!string.IsNullOrEmpty(_config?.MsBuildPadraoPath))
            {
                var selectedVersion = msBuildVersions.FirstOrDefault(v => v.Path == _config.MsBuildPadraoPath);
                if (selectedVersion != null)
                {
                    MsBuildVersionComboBox.SelectedItem = selectedVersion;
                }
            }
        }

        private async void MsBuildVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config != null && MsBuildVersionComboBox.SelectedItem is MsBuildInfo selectedVersion)
            {
                _config.MsBuildPadraoPath = selectedVersion.Path;
                await ConfigHelper.SalvarConfiguracoesAsync(_config);
            }
        }

        private async System.Threading.Tasks.Task CarregarConfiguracoes()
        {
            try
            {
                _config = await ConfigHelper.LerConfiguracoesAsync() ?? new ConfiguracoesdataObject();
                DiretorioBinarios.Text    = _config.DiretorioBinarios    ?? string.Empty;
                DiretorioProducao.Text    = _config.DiretorioProducao    ?? string.Empty;
                DiretorioEspecificos.Text = _config.DiretorioEspecificos ?? string.Empty;
                CaminhoCSReportImport.Text = _config.CaminhoCSReportImport ?? string.Empty;
                WesExePath.Text           = _config.WesExePath           ?? string.Empty;
                TglNotificarHoras.IsOn    = _config.NotificarHorasToggl;

                VerificarStatusNotificacoes();

                BServerServidor.Text = _config.BServerServidor ?? string.Empty;
                AtualizarStatusDll();

                _fonteBinariosChanging = true;
                FonteBinariosRadioButtons.SelectedItem = _config.FonteBinarios == JrTools.Enums.FonteBinarios.Jenkins
                    ? FonteBinariosJenkinsOption
                    : FonteBinariosServidorOption;
                _fonteBinariosChanging = false;
                CaminhoServidorBinarios.Text = _config.CaminhoServidorBinarios ?? string.Empty;
                JenkinsBaseUrl.Text = _config.JenkinsBaseUrl ?? string.Empty;
                JenkinsJobPath.Text = _config.JenkinsJobPath ?? string.Empty;
                AtualizarVisibilidadeFonteBinarios();

                AtualizarListViewBranches();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Erro ao carregar configurações",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();
                _config = new ConfiguracoesdataObject();
            }
        }

        private async void DiretorioBinarios_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.DiretorioBinarios = DiretorioBinarios.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
            AtualizarStatusDll();
        }

        private async void DiretorioProducao_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.DiretorioProducao = DiretorioProducao.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void DiretorioEspecificos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.DiretorioEspecificos = DiretorioEspecificos.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void CaminhoCSReportImport_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.CaminhoCSReportImport = CaminhoCSReportImport.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);

            var valido = CaminhoCSReportImport.Text.EndsWith("CSReportImport.exe", StringComparison.OrdinalIgnoreCase);
            ValidationInfoBar.Message = "O arquivo informado deve ser o CSReportImport.exe.";
            ValidationInfoBar.IsOpen = !valido && !string.IsNullOrWhiteSpace(CaminhoCSReportImport.Text);
        }

        private async void WesExePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.WesExePath = WesExePath.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void TglNotificarHoras_Toggled(object sender, RoutedEventArgs e)
        {
            if (_config == null) return;
            _config.NotificarHorasToggl = TglNotificarHoras.IsOn;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
            VerificarStatusNotificacoes();
        }

        private void VerificarStatusNotificacoes()
        {
            var setting = AppNotificationManager.Default.Setting;
            if (setting != AppNotificationSetting.Enabled)
            {
                NotificacaoStatusInfoBar.Severity = InfoBarSeverity.Warning;
                NotificacaoStatusInfoBar.Message = "Notificações bloqueadas pelo Windows. Acesse Configurações > Sistema > Notificações e ative para este app.";
                NotificacaoStatusInfoBar.IsOpen = true;
            }
            else
            {
                NotificacaoStatusInfoBar.IsOpen = false;
            }
        }

        private void BtnTestarNotificacao_Click(object sender, RoutedEventArgs e)
        {
            var setting = AppNotificationManager.Default.Setting;
            if (setting != AppNotificationSetting.Enabled)
            {
                NotificacaoStatusInfoBar.Severity = InfoBarSeverity.Error;
                NotificacaoStatusInfoBar.Message = $"Notificações bloqueadas ({setting}). Acesse Configurações do Windows > Sistema > Notificações e ative para este app.";
                NotificacaoStatusInfoBar.IsOpen = true;
                return;
            }

            try
            {
                NotificationService.Instance.ShowNotification("Teste JrTools", "Notificações estão funcionando!");
                NotificacaoStatusInfoBar.Severity = InfoBarSeverity.Success;
                NotificacaoStatusInfoBar.Message = "Notificação enviada com sucesso!";
                NotificacaoStatusInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                NotificacaoStatusInfoBar.Severity = InfoBarSeverity.Error;
                NotificacaoStatusInfoBar.Message = $"Erro ao enviar notificação: {ex.Message}";
                NotificacaoStatusInfoBar.IsOpen = true;
            }
        }

        #endregion

        #region Informações pessoais

        private async System.Threading.Tasks.Task CarregarDadosPessoais()
        {
            try
            {
                _dadosPessoais = await PerfilPessoalHelper.LerConfiguracoesAsync() ?? new DadosPessoaisDataObject();
                LoginRhWeb.Text = _dadosPessoais.LoginRhWeb ?? string.Empty;

                SenhaRhWeb.Password = _dadosPessoais.SenhaRhWeb ?? string.Empty;
                SenhaRhWebVisible.Text = new string('*', SenhaRhWeb.Password.Length);

                ApiGeminiPasswordBox.Password = _dadosPessoais.ApiGemini ?? string.Empty;
                ApiGeminiVisible.Text = new string('*', ApiGeminiPasswordBox.Password.Length);

                ApiTogglPasswordBox.Password = _dadosPessoais.ApiToggl ?? string.Empty;
                ApiTogglVisible.Text = new string('*', ApiTogglPasswordBox.Password.Length);

                // Siscon
                LoginSiscon.Text = _dadosPessoais.LoginSiscon ?? string.Empty;
                SenhaSiscon.Password = _dadosPessoais.SenhaSiscon ?? string.Empty;
                SenhaSisconVisible.Text = new string('*', SenhaSiscon.Password.Length);

                // Jenkins
                JenkinsUsuario.Text = _dadosPessoais.JenkinsUsuario ?? string.Empty;
                JenkinsApiTokenPasswordBox.Password = _dadosPessoais.JenkinsApiToken ?? string.Empty;
                JenkinsApiTokenVisible.Text = new string('*', JenkinsApiTokenPasswordBox.Password.Length);
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Erro ao carregar dados pessoais",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.ShowAsync();

                _dadosPessoais = new DadosPessoaisDataObject();
            }
        }

        private async void DadosPessoais_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dadosPessoais == null) return;
            _dadosPessoais.LoginRhWeb = LoginRhWeb.Text;
            _dadosPessoais.UrlRh = UrlRhWeb.Text;
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private async void SenhaSiteJR_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;

            string valor = _senhaVisible ? SenhaRhWebVisible.Text : SenhaRhWeb.Password;

            _dadosPessoais.SenhaRhWeb = valor;
            if (!_senhaVisible)
                SenhaRhWebVisible.Text = new string('*', valor.Length);
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private async void ApiTokenGemini_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;

            string valor = _geminiVisible ? ApiGeminiVisible.Text : ApiGeminiPasswordBox.Password;
            _dadosPessoais.ApiGemini = valor;
            if (!_geminiVisible)
                ApiGeminiVisible.Text = new string('*', valor.Length);
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private async void ApiToggl_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;

            string valor = _togglVisible ? ApiTogglVisible.Text : ApiTogglPasswordBox.Password;
            _dadosPessoais.ApiToggl = valor;
            if (!_togglVisible)
                ApiTogglVisible.Text = new string('*', valor.Length);
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        #endregion

        #region WES - BServer

        private void AtualizarStatusDll()
        {
            var dllPath = System.IO.Path.Combine(
                _config?.DiretorioBinarios ?? string.Empty,
                "delphi", "Benner.Tecnologia.BServer.Clients.dll");
            var existe = System.IO.File.Exists(dllPath);

            BServerDllInfoBar.Severity = existe ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            BServerDllInfoBar.Message  = existe ? "DLL disponível" : $"DLL não encontrada em: {dllPath}";
            BtnTestarBServer.IsEnabled = existe;

            if (!existe)
                BServerSistemaComboBox.Visibility = Visibility.Collapsed;
        }

        private async void BServerServidor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.BServerServidor = BServerServidor.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void BtnTestarBServer_Click(object sender, RoutedEventArgs e)
        {
            var servidor = BServerServidor.Text.Trim();
            if (string.IsNullOrWhiteSpace(servidor))
            {
                BServerStatusInfoBar.Severity = InfoBarSeverity.Error;
                BServerStatusInfoBar.Message = "Informe o endereço do servidor antes de testar.";
                BServerStatusInfoBar.IsOpen = true;
                return;
            }

            BtnTestarBServer.IsEnabled = false;
            BServerStatusInfoBar.Severity = InfoBarSeverity.Informational;
            BServerStatusInfoBar.Message = $"Conectando em {servidor}:2000...";
            BServerStatusInfoBar.IsOpen = true;
            BServerSistemaComboBox.Visibility = Visibility.Collapsed;

            try
            {
                var resultado = await BServerQueryService.ConsultarAsync(servidor, _config?.DiretorioBinarios ?? string.Empty);

                if (resultado.IsSuccess)
                {
                    BServerStatusInfoBar.Severity = InfoBarSeverity.Success;
                    BServerStatusInfoBar.Message =
                        $"Conectado! {resultado.AvailableSystems.Length} sistema(s) encontrado(s). " +
                        $"({resultado.ConnectionTime.TotalMilliseconds:0}ms)";

                    _bserverSistemaChanging = true;
                    BServerSistemaComboBox.ItemsSource = resultado.AvailableSystems;
                    BServerSistemaComboBox.SelectedItem = _config?.BServerSistema is string s
                        && resultado.AvailableSystems.Contains(s) ? s : null;
                    _bserverSistemaChanging = false;

                    BServerSistemaComboBox.Visibility = Visibility.Visible;
                }
                else
                {
                    BServerStatusInfoBar.Severity = InfoBarSeverity.Error;
                    BServerStatusInfoBar.Message = resultado.ErrorMessage;
                }
            }
            finally
            {
                BtnTestarBServer.IsEnabled = true;
            }
        }

        private async void BServerSistemaComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bserverSistemaChanging || _config == null) return;
            _config.BServerSistema = BServerSistemaComboBox.SelectedItem as string;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        #endregion

        #region Fonte de binários

        private void AtualizarVisibilidadeFonteBinarios()
        {
            var jenkinsAtivo = ReferenceEquals(FonteBinariosRadioButtons.SelectedItem, FonteBinariosJenkinsOption);
            CaminhoServidorBinarios.Visibility = jenkinsAtivo ? Visibility.Collapsed : Visibility.Visible;
            JenkinsConfigPanel.Visibility = jenkinsAtivo ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void FonteBinariosRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AtualizarVisibilidadeFonteBinarios();

            if (_fonteBinariosChanging || _config == null) return;
            _config.FonteBinarios = ReferenceEquals(FonteBinariosRadioButtons.SelectedItem, FonteBinariosJenkinsOption)
                ? JrTools.Enums.FonteBinarios.Jenkins
                : JrTools.Enums.FonteBinarios.Servidor;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void CaminhoServidorBinarios_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.CaminhoServidorBinarios = CaminhoServidorBinarios.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void JenkinsBaseUrl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.JenkinsBaseUrl = JenkinsBaseUrl.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void JenkinsJobPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.JenkinsJobPath = JenkinsJobPath.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void JenkinsUsuario_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dadosPessoais == null) return;
            _dadosPessoais.JenkinsUsuario = JenkinsUsuario.Text;
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private async void JenkinsApiToken_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;

            string valor = _jenkinsTokenVisible ? JenkinsApiTokenVisible.Text : JenkinsApiTokenPasswordBox.Password;
            _dadosPessoais.JenkinsApiToken = valor;
            if (!_jenkinsTokenVisible)
                JenkinsApiTokenVisible.Text = new string('*', valor.Length);
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private void ToggleJenkinsApiTokenVisibility_Click(object sender, RoutedEventArgs e)
        {
            _jenkinsTokenVisible = !_jenkinsTokenVisible;
            if (_jenkinsTokenVisible)
            {
                JenkinsApiTokenVisible.Visibility = Visibility.Visible;
                JenkinsApiTokenPasswordBox.Visibility = Visibility.Collapsed;
                JenkinsApiTokenVisible.Text = _dadosPessoais.JenkinsApiToken ?? "";
            }
            else
            {
                JenkinsApiTokenVisible.Visibility = Visibility.Collapsed;
                JenkinsApiTokenPasswordBox.Visibility = Visibility.Visible;
                JenkinsApiTokenPasswordBox.Password = _dadosPessoais.JenkinsApiToken ?? "";
                JenkinsApiTokenVisible.Text = new string('*', JenkinsApiTokenPasswordBox.Password.Length);
            }
        }

        private async void BtnTestarJenkins_Click(object sender, RoutedEventArgs e)
        {
            var baseUrl = JenkinsBaseUrl.Text.Trim();
            var jobPath = JenkinsJobPath.Text.Trim();
            var usuario = JenkinsUsuario.Text.Trim();
            var token   = _jenkinsTokenVisible ? JenkinsApiTokenVisible.Text : JenkinsApiTokenPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(jobPath) ||
                string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(token))
            {
                JenkinsStatusInfoBar.Severity = InfoBarSeverity.Error;
                JenkinsStatusInfoBar.Message = "Preencha URL, caminho do job, usuário e token antes de testar.";
                JenkinsStatusInfoBar.IsOpen = true;
                return;
            }

            BtnTestarJenkins.IsEnabled = false;
            JenkinsStatusInfoBar.Severity = InfoBarSeverity.Informational;
            JenkinsStatusInfoBar.Message = "Conectando...";
            JenkinsStatusInfoBar.IsOpen = true;

            try
            {
                var provider = new JenkinsBinarioProvider(baseUrl, jobPath, usuario, token);
                var ok = await provider.TestarConexaoAsync();

                JenkinsStatusInfoBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                JenkinsStatusInfoBar.Message = ok
                    ? "Conectado com sucesso!"
                    : "Não foi possível autenticar. Confira usuário, token e o caminho do job.";
            }
            catch (Exception ex)
            {
                JenkinsStatusInfoBar.Severity = InfoBarSeverity.Error;
                JenkinsStatusInfoBar.Message = $"Erro ao conectar: {ex.Message}";
            }
            finally
            {
                BtnTestarJenkins.IsEnabled = true;
            }
        }

        #endregion

        #region Branches

        private void AtualizarListViewBranches()
        {
            ListViewBranches.ItemsSource = null;
            ListViewBranches.ItemsSource = _config.ListaBranches
                .Where(b => b != "Outro")
                .ToList();
        }

        private async void BtnAdicionarBranch_Click(object sender, RoutedEventArgs e)
        {
            var nova = TxtNovaBranch.Text.Trim();
            if (string.IsNullOrWhiteSpace(nova)) return;

            if (!_config.ListaBranches.Contains(nova, StringComparer.OrdinalIgnoreCase))
            {
                var idxOutro = _config.ListaBranches.IndexOf("Outro");
                if (idxOutro >= 0)
                    _config.ListaBranches.Insert(idxOutro, nova);
                else
                    _config.ListaBranches.Add(nova);

                await ConfigHelper.SalvarConfiguracoesAsync(_config);
                AtualizarListViewBranches();
            }

            TxtNovaBranch.Text = string.Empty;
        }

        private async void BtnExcluirBranch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not string branch) return;
            _config.ListaBranches.Remove(branch);
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
            AtualizarListViewBranches();
        }

        private void TxtNovaBranch_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                BtnAdicionarBranch_Click(sender, e);
        }

        #endregion

        private void ToggleSenhaVisibility_Click(object sender, RoutedEventArgs e)
        {
            _senhaVisible = !_senhaVisible;
            if (_senhaVisible)
            {
                SenhaRhWebVisible.Visibility = Visibility.Visible;
                SenhaRhWeb.Visibility = Visibility.Collapsed;
                SenhaRhWebVisible.Text = _dadosPessoais.SenhaRhWeb ?? "";
            }
            else
            {
                SenhaRhWebVisible.Visibility = Visibility.Collapsed;
                SenhaRhWeb.Visibility = Visibility.Visible;
                SenhaRhWeb.Password = _dadosPessoais.SenhaRhWeb ?? "";
                SenhaRhWebVisible.Text = new string('*', SenhaRhWeb.Password.Length);
            }
        }

        private void ToggleApiGeminiVisibility_Click(object sender, RoutedEventArgs e)
        {
            _geminiVisible = !_geminiVisible;
            if (_geminiVisible)
            {
                ApiGeminiVisible.Visibility = Visibility.Visible;
                ApiGeminiPasswordBox.Visibility = Visibility.Collapsed;
                ApiGeminiVisible.Text = _dadosPessoais.ApiGemini ?? "";
            }
            else
            {
                ApiGeminiVisible.Visibility = Visibility.Collapsed;
                ApiGeminiPasswordBox.Visibility = Visibility.Visible;
                ApiGeminiPasswordBox.Password = _dadosPessoais.ApiGemini ?? "";
                ApiGeminiVisible.Text = new string('*', ApiGeminiPasswordBox.Password.Length);
            }
        }

        private void ToggleApiTogglVisibility_Click(object sender, RoutedEventArgs e)
        {
            _togglVisible = !_togglVisible;
            if (_togglVisible)
            {
                ApiTogglVisible.Visibility = Visibility.Visible;
                ApiTogglPasswordBox.Visibility = Visibility.Collapsed;
                ApiTogglVisible.Text = _dadosPessoais.ApiToggl ?? "";
            }
            else
            {
                ApiTogglVisible.Visibility = Visibility.Collapsed;
                ApiTogglPasswordBox.Visibility = Visibility.Visible;
                ApiTogglPasswordBox.Password = _dadosPessoais.ApiToggl ?? "";
                ApiTogglVisible.Text = new string('*', ApiTogglPasswordBox.Password.Length);
            }
        }

        #region Siscon

        private async void SisconDadosPessoais_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dadosPessoais == null) return;
            _dadosPessoais.LoginSiscon = LoginSiscon.Text;
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private async void SisconSenha_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;
            string valor = _senhaSisconVisible ? SenhaSisconVisible.Text : SenhaSiscon.Password;
            _dadosPessoais.SenhaSiscon = valor;
            if (!_senhaSisconVisible)
                SenhaSisconVisible.Text = new string('*', valor.Length);
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }

        private void ToggleSenhaSisconVisibility_Click(object sender, RoutedEventArgs e)
        {
            _senhaSisconVisible = !_senhaSisconVisible;
            if (_senhaSisconVisible)
            {
                SenhaSisconVisible.Visibility = Visibility.Visible;
                SenhaSiscon.Visibility = Visibility.Collapsed;
                SenhaSisconVisible.Text = _dadosPessoais.SenhaSiscon ?? "";
            }
            else
            {
                SenhaSisconVisible.Visibility = Visibility.Collapsed;
                SenhaSiscon.Visibility = Visibility.Visible;
                SenhaSiscon.Password = _dadosPessoais.SenhaSiscon ?? "";
                SenhaSisconVisible.Text = new string('*', SenhaSiscon.Password.Length);
            }
        }

        #endregion

    }
}
