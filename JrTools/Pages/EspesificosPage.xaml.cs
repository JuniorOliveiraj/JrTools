using JrTools.Dto;
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
    public sealed partial class EspesificosPage : Page
    {
        public List<string> ListaDeProjetos { get; set; }

        public EspesificosPage()
        {
            InitializeComponent();
            CarregarProjetos();
        }


        private void CarregarProjetos()
        {
            ListaDeProjetos = new List<string>
            {
                "caj",
                "rh" 
            };

            ProjetoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoComboBox.SelectedIndex = 0;
        }



        private async void ProcessarButton_Click(object sender, RoutedEventArgs e)
        {
            // Esconde a barra de erro antes de validar
            ValidationInfoBar.IsOpen = false;

            // Validação dos campos de texto
            if (string.IsNullOrWhiteSpace(EnderecoServidorTextBox.Text) ||
                string.IsNullOrWhiteSpace(UsuarioInternoTextBox.Text) ||
                string.IsNullOrWhiteSpace(SenhaInternoPasswordBox.Password) ||
                string.IsNullOrWhiteSpace(SiteTextBox.Text) ||
                string.IsNullOrWhiteSpace(NomeAplicacaoTextBox.Text) ||
                string.IsNullOrWhiteSpace(PoolTextBox.Text) ||
                string.IsNullOrWhiteSpace(NumeroProvedoresTextBox.Text) ||
                string.IsNullOrWhiteSpace(NomeSistemaBennerTextBox.Text))
            {
                ValidationInfoBar.Title = "Campos Obrigatórios";
                ValidationInfoBar.Message = "Por favor, preencha todos os campos antes de processar.";
                ValidationInfoBar.IsOpen = true;
                return; // Para a execução se a validação falhar
            }

            // Se a validação passar, cria o objeto com os dados da tela
            var config = new PageEspesificosDataObject
            {
                // Projeto (agora pega o item selecionado diretamente da lista)
                Projeto = ProjetoComboBox.SelectedItem as string,

                // Opções de Compilação
                BaixarBinario = BaixarBinarioToggle.IsOn,
                CriarAtalho = CriarAtalhoToggle.IsOn,
                CompilarEspecificos = CompilarEspecificosToggle.IsOn,
                CriarAplicacaoIIS = CriarAplicacaoIISToggle.IsOn,
                RestaurarWebApp = RestaurarWebAppToggle.IsOn,

                // Configuração da Aplicação Web
                EnderecoServidor = EnderecoServidorTextBox.Text,
                UsuarioInterno = UsuarioInternoTextBox.Text,
                SenhaInterno = SenhaInternoPasswordBox.Password,

                // Outras Informações
                Site = SiteTextBox.Text,
                NomeAplicacao = NomeAplicacaoTextBox.Text,
                Pool = PoolTextBox.Text,
                NumeroProvedores = NumeroProvedoresTextBox.Text,
                NomeSistemaBenner = NomeSistemaBennerTextBox.Text
            };

            // Exemplo de uso do objeto: exibe um diálogo de sucesso
            /*  var dialog = new ContentDialog
              {
                  Title = "Sucesso",
                  Content = $"Objeto de configuração criado para o projeto: {config.Projeto}. Agora você pode usar este objeto para o processamento.",
                  CloseButtonText = "Ok"
              };

              await dialog.ShowAsync();*/
        }
    }
}
