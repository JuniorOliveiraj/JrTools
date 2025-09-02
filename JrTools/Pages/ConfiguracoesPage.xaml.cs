using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace JrTools.Pages
{
    public sealed partial class ConfiguracoesPage : Page
    {
        private ConfiguracoesdataObject _config;

        public ConfiguracoesPage()
        {
            this.InitializeComponent();
            // Executa após a página carregar
            this.Loaded += ConfiguracoesPage_Loaded;
        }

        private async void ConfiguracoesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarConfiguracoes();
        }

        private async System.Threading.Tasks.Task CarregarConfiguracoes()
        {
            try
            {
                _config = await ConfigHelper.LerConfiguracoesAsync();

                if (_config == null)
                    _config = new ConfiguracoesdataObject();

                // Seta os controles com segurança
                ProjetoComboBox.SelectedItem = _config.ProjetoSelecionado ?? "Default";
                DiretorioBinarios.Text = _config.DiretorioBinarios ?? string.Empty;
                DiretorioProducao.Text = _config.DiretorioProducao ?? string.Empty;
                DiretorioEspesificos.Text = _config.DiretorioEspecificos ?? string.Empty;
            }
            catch (Exception ex)
            {
                ContentDialog erroDialog = new ContentDialog
                {
                    Title = "Erro ao carregar configurações",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                _ = erroDialog.ShowAsync();

                _config = new ConfiguracoesdataObject(); // fallback seguro
            }
        }

        // Chamado quando o ComboBox muda
        private async void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config == null) return;

            _config.ProjetoSelecionado = ProjetoComboBox.SelectedItem?.ToString();
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        // Chamado quando os TextBox mudam
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
    }
}
