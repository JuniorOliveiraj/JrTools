using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace JrTools.Pages
{
    public sealed partial class ConfiguracoesPage : Page
    {
        private ConfiguracoesdataObject _config;
        private DadosPessoaisDataObject _dadosPessoais;

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
        }

        #region Parametrizações

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

                LoginSiteJR.Text = _dadosPessoais.LoginDevSite ?? string.Empty;
                SenhaSiteJR.AccessKey = _dadosPessoais.SenhaDevSite ?? string.Empty;
                LoginRhWeb.Text = _dadosPessoais.LoginRhWeb ?? string.Empty;
                SenhaRhWeb.AccessKey = _dadosPessoais.SenhaRhWeb ?? string.Empty;
                ApiGeminiPasswordBox.AccessKey = _dadosPessoais.ApiGemini ?? string.Empty;
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

            _dadosPessoais.LoginDevSite = LoginSiteJR.Text;
             
            _dadosPessoais.LoginRhWeb = LoginRhWeb.Text;
            

            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }
        private async void SenhaSiteJR_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_dadosPessoais == null) return;
            _dadosPessoais.SenhaDevSite = SenhaSiteJR.AccessKey;
            _dadosPessoais.SenhaRhWeb = SenhaRhWeb.AccessKey;
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }



        private async void ApiTokenGemini_PasswordChanged(object sender, RoutedEventArgs e)
        {
             _dadosPessoais.ApiGemini = ApiGeminiPasswordBox.Password;
            await PerfilPessoalHelper.SalvarConfiguracoesAsync(_dadosPessoais);
        }


        #endregion
    }
}
