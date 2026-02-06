using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace JrTools.Pages
{
    public sealed partial class ConfiguracoesPage : Page
    {
        private ConfiguracoesdataObject _config;
        private DadosPessoaisDataObject _dadosPessoais;

        private bool _senhaVisible = false;
        private bool _geminiVisible = false;
        private bool _togglVisible = false;

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
        }

        #region Parametrizações

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
                DiretorioBinarios.Text = _config.DiretorioBinarios ?? string.Empty;
                DiretorioProducao.Text = _config.DiretorioProducao ?? string.Empty;
                DiretorioEspesificos.Text = _config.DiretorioEspecificos ?? string.Empty;
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
        }

        private async void DiretorioProducao_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.DiretorioProducao = DiretorioProducao.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        private async void DiretorioEspesificos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_config == null) return;
            _config.DiretorioEspecificos = DiretorioEspesificos.Text;
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
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

        #region Toggle Visibilidade

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

        #endregion
    }
}
