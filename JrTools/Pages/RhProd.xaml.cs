using JrTools.Dto;
using JrTools.Negocios;
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
    public sealed partial class RhProdPage : Page
    {
        public List<string> ListaDeProjetos { get; set; }
        public RhProdPage()
        {
            InitializeComponent();
            CarregarProjetos();
        }
        private void CarregarProjetos()
        {
            ListaDeProjetos = new List<string>
            {
                "producao_09.00",
                "producao_08.06",
                "producao_08.05",
                "producao_08.04",
                "dev-09.00.00",
                "dev-08.06.00",
                "dev-08.05.00",
                "dev-08.04.00",
                "Outro"
            };

            ProjetoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoComboBox.SelectedIndex = 0;
        }

        private async void ProcessarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Limpa logs anteriores e esconde mensagens de validação
                TerminalOutput.Text = "[INFO] Iniciando processamento...\n";
                ValidationInfoBar.IsOpen = false;

                // Monta o DTO com os parâmetros da UI
                var dto = new PageProdutoDataObject
                {
                    Breach = ProjetoComboBox.SelectedItem?.ToString(),
                    AtualizarBinarios = BaixarBinarioToggle.IsOn,
                    BuildarProjeto = CompilarEspecificosToggle.IsOn,
                    AtualizarBreach = GitPull.IsOn,
                    BreachEspesificaDeTrabalho = ProjetoComboBox.SelectedItem?.ToString() == "Outro"
                                                ? breachEspesifica.Text
                                                : null
                };

                // Validação se "Outro" foi selecionado
                if (ProjetoComboBox.SelectedItem?.ToString() == "Outro" && string.IsNullOrWhiteSpace(breachEspesifica.Text))
                {
                    ValidationInfoBar.Message = "Informe um valor para o campo 'Breach'.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }

                // Cria o objeto de progresso para receber logs em tempo real
                var progresso = new Progress<string>(log =>
                {
                    TerminalOutput.Text += log + "\n";
                    ;// TerminalOutput.ScrollToEnd(); // Faz a rolagem automática
                });

                // Executa o fluxo principal
                var flow = new RhProdFlow();
                var (success, logs, errorMessage) = await flow.ExecutarAsync(dto, progresso);

                // Exibe logs finais
                TerminalOutput.Text += logs;

                // Exibe mensagem de erro, se houver
                if (!success)
                {
                    ValidationInfoBar.Message = errorMessage;
                    ValidationInfoBar.IsOpen = true;
                }
                else
                {
                    TerminalOutput.Text += "[INFO] Processamento concluído com sucesso!\n";
                }
            }
            catch (Exception ex)
            {
                // Captura exceções inesperadas
                ValidationInfoBar.Message = $"Ocorreu um erro inesperado: {ex.Message}";
                ValidationInfoBar.IsOpen = true;
                TerminalOutput.Text += $"[ERRO] {ex}\n";
            }
        }



        private void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoComboBox.SelectedItem?.ToString() == "Outro")
            {
                breachEspesifica.Visibility = Visibility.Visible;
            }
            else
            {
                breachEspesifica.Visibility = Visibility.Collapsed;
                breachEspesifica.Text = string.Empty;  
            }
        }
    }
}
