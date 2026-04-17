using JrTools.Dto;
using JrTools.Negocios;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class RhProdPage : Page
    {
        private const int MAX_TERMINAL_LENGTH = 15000;
        private readonly ProcessGuardianService _processGuardian;

        public List<string> ListaDeProjetos { get; set; }

        public RhProdPage()
        {
            InitializeComponent();
            _processGuardian = new ProcessGuardianService();
            Loaded += async (_, _) => await CarregarProjetosAsync();
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await CarregarProjetosAsync();
        }

        private async Task CarregarProjetosAsync()
        {
            var cfg = await ConfigHelper.LerConfiguracoesAsync();
            ListaDeProjetos = cfg?.ListaBranches?.Count > 0
                ? new List<string>(cfg.ListaBranches)
                : new List<string> { "prd/09.00", "dev/09.00.00", "Outro" };

            if (!ListaDeProjetos.Contains("Outro"))
                ListaDeProjetos.Add("Outro");

            var selecionado = ProjetoComboBox.SelectedItem?.ToString();
            ProjetoComboBox.ItemsSource = ListaDeProjetos;

            var idx = selecionado != null ? ListaDeProjetos.IndexOf(selecionado) : 0;
            ProjetoComboBox.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private async void ProcessarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetUiState(processing: true);

                TerminalOutput.Text = "[INFO] Iniciando processamento...\n";
                ValidationInfoBar.IsOpen = false;

                if (!ValidarEntradas(out var dto)) return;

                string[] processosParaMonitorar = { "BPrv230", "CS1", "Builder" };

                var uiProgress = new Progress<string>(AppendTerminalLog);

                await _processGuardian.ExecutarComProcessosFechadosAsync(
                    processosParaMonitorar,
                    async (progresso) => await ExecutarFluxoRhProdAsync(dto, progresso),
                    uiProgress
                );
            }
            catch (Exception ex)
            {
                ShowError($"Ocorreu um erro inesperado: {ex.Message}");
                AppendTerminalLog($"[ERRO CRÍTICO] {ex}");
            }
            finally
            {
                SetUiState(processing: false);
            }
        }

        private async Task ExecutarFluxoRhProdAsync(PageProdutoDataObject dto, IProgress<string> progresso)
        {
            var flow = new RhProdFlow();
            var (success, logs, errorMessage) = await flow.ExecutarAsync(dto, progresso);

            if (!success)
                DispatcherQueue.TryEnqueue(() => ShowError(errorMessage));
            else
            {
                progresso.Report("[INFO] Processamento concluído com sucesso!");
                AbrirNavegador();
            }
        }

        private bool ValidarEntradas(out PageProdutoDataObject dto)
        {
            dto = null;
            var selectedProjeto = ProjetoComboBox.SelectedItem?.ToString();

            if (selectedProjeto == "Outro" && string.IsNullOrWhiteSpace(breachEspecifica.Text))
            {
                ShowError("Informe um valor para o campo 'Branch' quando selecionar 'Outro'.");
                return false;
            }

            dto = new PageProdutoDataObject
            {
                Breach                     = selectedProjeto,
                AtualizarBinarios          = BaixarBinarioToggle.IsOn,
                BuildarProjeto             = CompilarEspecificosToggle.IsOn,
                AtualizarBreach            = GitPull.IsOn,
                BreachEspecificaDeTrabalho = selectedProjeto == "Outro" ? breachEspecifica.Text : string.Empty,
                TagEspecificaDeTrabalho    = selectedProjeto == "Outro"
                    ? (tagEspecifica.Text == "Nenhum" ? string.Empty : tagEspecifica.Text)
                    : string.Empty,
                RunnerFechado  = false,
                BuilderFechado = false,
                PrividerFechado = false
            };

            return true;
        }

        private void AbrirNavegador()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = "http://localhost/prod",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"[WARN] Não foi possível abrir o navegador: {ex.Message}");
            }
        }

        private void SetUiState(bool processing)
        {
            ProcessarButton2.IsEnabled = !processing;
            LoadingRing.IsActive       = processing;
        }

        private void ShowError(string message)
        {
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen  = true;
        }

        private void AppendTerminalLog(string mensagem)
        {
            if (DispatcherQueue.HasThreadAccess)
                AtualizarTextoTerminal(mensagem);
            else
                DispatcherQueue.TryEnqueue(() => AtualizarTextoTerminal(mensagem));
        }

        private void AtualizarTextoTerminal(string mensagem)
        {
            TerminalOutput.Text += mensagem + "\n";
            if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
                TerminalOutput.Text = TerminalOutput.Text[^MAX_TERMINAL_LENGTH..];
            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
        }

        private void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var isOutro    = ProjetoComboBox.SelectedItem?.ToString() == "Outro";
            var visibility = isOutro ? Visibility.Visible : Visibility.Collapsed;

            breachEspecifica.Visibility = visibility;
            tagEspecifica.Visibility    = visibility;

            if (!isOutro)
            {
                breachEspecifica.Text = string.Empty;
                tagEspecifica.Text    = string.Empty;
            }
            else
            {
                tagEspecifica.Text = "Nenhum";
            }
        }
    }
}
