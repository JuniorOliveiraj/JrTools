using JrTools.Dto;
using JrTools.Negocios;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class RhProdPage : Page
    {
        public List<string> ListaDeProjetos { get; set; }

        private const int MAX_TERMINAL_LENGTH = 15000;

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
                ProcessarButton2.IsEnabled = false;
                LoadingRing.IsActive = true;

                TerminalOutput.Text = "[INFO] Iniciando processamento...\n";
                ValidationInfoBar.IsOpen = false;

                // Cria DTO
                var selectedProjeto = ProjetoComboBox.SelectedItem?.ToString();

                var dto = new PageProdutoDataObject
                {
                    Breach = selectedProjeto,
                    AtualizarBinarios = BaixarBinarioToggle.IsOn,
                    BuildarProjeto = CompilarEspecificosToggle.IsOn,
                    AtualizarBreach = GitPull.IsOn,
                    BreachEspecificaDeTrabalho = selectedProjeto == "Outro"
                                                    ? breachEspecifica.Text
                                                    : string.Empty,
                    TagEspecificaDeTrabalho = selectedProjeto == "Outro"
                                                    ? (tagEspecifica.Text == "Nenhum" ? string.Empty : tagEspecifica.Text)
                                                    : string.Empty
                };


                if (ProjetoComboBox.SelectedItem?.ToString() == "Outro" && string.IsNullOrWhiteSpace(breachEspecifica.Text))
                {
                    ValidationInfoBar.Message = "Informe um valor para o campo 'Breach'.";
                    ValidationInfoBar.IsOpen = true;
                    return;
                }

                string[] processos = { "BPrv230", "CS1", "Builder" };

                // Executa o fluxo mantendo os processos fechados
                var flow = new RhProdFlow();
                await ExecutarComProcessosFechadosAsync(processos, async progresso =>
                {
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
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "http://localhost/prod",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                        catch (Exception ex)
                        {
                            AppendTerminalLog($"[WARN] Não foi possível abrir o navegador: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ValidationInfoBar.Message = $"Ocorreu um erro inesperado: {ex.Message}";
                ValidationInfoBar.IsOpen = true;
                AppendTerminalLog($"[ERRO] {ex}");
            }
            finally
            {
                ProcessarButton2.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }

        /// <summary>
        /// Executa uma ação enquanto mantém os processos fechados.
        /// </summary>
        private async Task ExecutarComProcessosFechadosAsync(string[] processos, Func<IProgress<string>, Task> acao)
        {
            using var cts = new CancellationTokenSource();
            var progresso = new Progress<string>(msg => AppendTerminalLog(msg));
            var manterFechadoTask = ManterProcessosFechadosAsync(processos, progresso, cts.Token);

            try
            {
                await acao(progresso);
            }
            finally
            {
                cts.Cancel();
                await manterFechadoTask;
            }
        }

        /// <summary>
        /// Mantém os processos fechados em loop até que o token seja cancelado.
        /// Só tenta matar processos que estejam rodando.
        /// </summary>
        private async Task ManterProcessosFechadosAsync(string[] processos, IProgress<string> progresso, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var nomeProcesso in processos)
                {
                    try
                    {
                        int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso);

                        if (qtd > 0) // Só tenta matar se houver processo rodando
                        {
                            await ProcessKiller.KillByNameAsync(nomeProcesso, progresso);
                            progresso.Report($"{nomeProcesso} restante após tentativa de kill: {qtd}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Só loga, não quebra o app
                        progresso.Report($"[WARN] Não foi possível matar {nomeProcesso}: {ex.Message}");
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Adiciona logs no terminal com limite e rolagem automática
        /// </summary>
        private void AppendTerminalLog(string mensagem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TerminalOutput.Text += mensagem + "\n";

                if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
                {
                    TerminalOutput.Text = TerminalOutput.Text.Substring(TerminalOutput.Text.Length - MAX_TERMINAL_LENGTH);
                }

                TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
            });
        }

        private void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoComboBox.SelectedItem?.ToString() == "Outro")
            {
                breachEspecifica.Visibility = Visibility.Visible;
                tagEspecifica.Visibility = Visibility.Visible;
                tagEspecifica.Text = "Nenhum";
            }
            else
            {
                breachEspecifica.Visibility = Visibility.Collapsed;
                breachEspecifica.Text = string.Empty;

                tagEspecifica.Visibility = Visibility.Collapsed;
                tagEspecifica.Text = string.Empty;
            }
        }
    }
}
