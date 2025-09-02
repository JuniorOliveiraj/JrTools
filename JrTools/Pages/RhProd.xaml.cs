using JrTools.Dto;
using JrTools.Negocios;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace JrTools.Pages
{
    public sealed partial class RhProdPage : Page
    {
        public List<string> ListaDeProjetos { get; set; }

        // Limite máximo de caracteres no terminal
        private const int MAX_TERMINAL_LENGTH = 15000; // grande, mas mantém performance

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
                // Bloqueia botões e ativa loading
                ProcessarButton2.IsEnabled = false;
                LoadingRing.IsActive = true;

                TerminalOutput.Text = "[INFO] Iniciando processamento...\n";
                ValidationInfoBar.IsOpen = false;

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

                if (ProjetoComboBox.SelectedItem?.ToString() == "Outro" && string.IsNullOrWhiteSpace(breachEspesifica.Text))
                {
                    ValidationInfoBar.Message = "Informe um valor para o campo 'Breach'.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }

                // Progress para atualizar terminal sem travar UI
                var progresso = new Progress<string>(log => AppendTerminalLog(log));

                var flow = new RhProdFlow();
                var (success, logs, errorMessage) = await flow.ExecutarAsync(dto, progresso);

                AppendTerminalLog(logs);

                if (!success)
                {
                    ValidationInfoBar.Message = errorMessage;
                    ValidationInfoBar.IsOpen = true;
                }
                else
                {
                    AppendTerminalLog("[INFO] Processamento concluído com sucesso!");
                }
            }
            catch (Exception ex)
            {
                ValidationInfoBar.Message = $"Ocorreu um erro inesperado: {ex.Message}";
                ValidationInfoBar.IsOpen = true;
                AppendTerminalLog($"[ERRO] {ex}");
            }
            finally
            {
                // Reativa botões e desativa loading
                ProcessarButton2.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }

        // Adiciona logs no terminal com limite e rolagem automática
        private void AppendTerminalLog(string mensagem)
        {
            TerminalOutput.Text += mensagem + "\n";

            if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
            {
                TerminalOutput.Text = TerminalOutput.Text.Substring(TerminalOutput.Text.Length - MAX_TERMINAL_LENGTH);
            }

            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
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
