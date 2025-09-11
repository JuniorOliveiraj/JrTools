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
    public sealed partial class EspesificosPage : Page
    {
        private ConfiguracoesdataObject _config;
        public List<PastaInformacoesDto> ListaDeProjetos { get; set; }
        private const int MAX_TERMINAL_LENGTH = 15000;

        public EspesificosPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            this.Loaded += ConfiguracoesPage_Loaded;

        }


        private void CarregarProjetos()
        {
            var direto = _config?.DiretorioEspecificos;
            ListaDeProjetos = Folders.ListarPastas(direto); 


            ProjetoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoComboBox.DisplayMemberPath = "Nome";
        }

        private async void ConfiguracoesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarConfiguracoes();
            CarregarProjetos();
        }

        private async System.Threading.Tasks.Task CarregarConfiguracoes()
        {
            try
            {
                _config = await ConfigHelper.LerConfiguracoesAsync();
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
            var progresso = new Progress<string>(msg =>
            {
                AppendTerminalLog(msg);
            });


            await ProcessKiller.ListProcessAsync(progresso);
            // Exemplo de uso do objeto: exibe um diálogo de sucesso
            /*  var dialog = new ContentDialog
              {
                  Title = "Sucesso",
                  Content = $"Objeto de configuração criado para o projeto: {config.Projeto}. Agora você pode usar este objeto para o processamento.",
                  CloseButtonText = "Ok"
              };

              await dialog.ShowAsync();*/
        }


        private void AppendTerminalLog(string mensagem)
        {
            TerminalOutput.Text += mensagem + "\n";

            if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
            {
                TerminalOutput.Text = TerminalOutput.Text.Substring(TerminalOutput.Text.Length - MAX_TERMINAL_LENGTH);
            }

            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
        }
    }
}
