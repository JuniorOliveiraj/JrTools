using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JrTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfiguracoesPage : Page
    {
        private ConfiguracoesdataObject _config;

        public ConfiguracoesPage()
        {
            this.InitializeComponent();
            CarregarConfiguracoes();
        }

        private async void CarregarConfiguracoes()
        {
            _config = await ConfigHelper.LerConfiguracoesAsync();

            ProjetoComboBox.SelectedItem = _config.ProjetoSelecionado;
            DiretorioBinarios.Text = _config.DiretorioBinarios;
            DiretorioProducao.Text = _config.DiretorioProducao;
            DiretorioEspesificos.Text = _config.DiretorioEspecificos;
        }

        // Chamado quando o ComboBox muda
        private async void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config == null) return;

            _config.ProjetoSelecionado = ProjetoComboBox.SelectedItem?.ToString();
            await ConfigHelper.SalvarConfiguracoesAsync(_config);
        }

        // Chamado quando um TextBox muda
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
