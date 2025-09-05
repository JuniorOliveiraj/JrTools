using JrTools.Dto;
using JrTools.Flows;
using JrTools.Negocios;
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
    public sealed partial class BuildarProjeto : Page
    {
        private ConfiguracoesdataObject _config;
        public List<PastaInformacoesDto> ListaDeProjetos { get; set; }
        public List<SolucaoInformacoesDto> ListaSolucoes { get; set; }

        private const int MAX_TERMINAL_LENGTH = 15000;
        public BuildarProjeto()
        {
            InitializeComponent();
            ExpanderDotNet.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, Expander_IsExpandedChanged);
            ExpanderDelphi.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, Expander_IsExpandedChanged);
            this.Loaded += ConfiguracoesPage_Loaded;

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
        private void CarregarProjetos()
        { 
            var direto = _config?.DiretorioEspecificos ;
            ListaDeProjetos = Folders.ListarPastas(direto);
            ProjetoDotnetselecionadoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoDotnetselecionadoComboBox.DisplayMemberPath = "Nome";

            var projetoProd = ListaDeProjetos.FirstOrDefault(p =>
                string.Equals(p.Nome, "prod", StringComparison.OrdinalIgnoreCase));

            if (projetoProd != null)
            {
                ProjetoDotnetselecionadoComboBox.SelectedItem = projetoProd;
            }
            else
            {
                ProjetoDotnetselecionadoComboBox.SelectedIndex = 0; 
            }
        }


     
        private void ProjetoDotnetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoDotnetselecionadoComboBox.SelectedItem is PastaInformacoesDto projetoSelecionado)
            {
                string caminhoProjeto = projetoSelecionado.Caminho;

                ListaSolucoes = Folders.ListarArquivosSln(caminhoProjeto);

                SolucaoDotnetSelecionadoComboBox.ItemsSource = ListaSolucoes;
                SolucaoDotnetSelecionadoComboBox.DisplayMemberPath = "Nome";

                // Seleciona a solução "Compilacao_Completa_SemWebApp.sln" se existir
                var solucaoDefault = ListaSolucoes.FirstOrDefault(s =>
                    string.Equals(s.Nome, "Compilacao_Completa_SemWebApp.sln", StringComparison.OrdinalIgnoreCase));

                if (solucaoDefault != null)
                {
                    SolucaoDotnetSelecionadoComboBox.SelectedItem = solucaoDefault;
                }
                else
                {
                    SolucaoDotnetSelecionadoComboBox.SelectedIndex = ListaSolucoes.Any() ? 0 : -1; // fallback
                }
            }
        }

        private async void ProcessarDotnetButton_Click(object sender, RoutedEventArgs e)
        {
            if (SolucaoDotnetSelecionadoComboBox.SelectedItem is not SolucaoInformacoesDto solucaoSelecionada)
            {
                ShowValidationError("Selecione uma solução antes de processar.");
                return;
            }

            // Desabilita botão e ativa ProgressRing
            BuildarDotnetButton.IsEnabled = false;
            LoadingRing.IsActive = true;

            try
            {
                string caminhoSln = solucaoSelecionada.Caminho;
                var buildHandler = new BinldarProjetoSrv();

                var progresso = new Progress<string>(msg =>
                {
                    AppendTerminalLog(msg);
                });

                AppendTerminalLog($"Iniciando build da solução: {solucaoSelecionada.Nome}");
                await buildHandler.BuildarProjetoAsync(caminhoSln, progresso);
                AppendTerminalLog("Build concluído com sucesso!");
            }
            catch (Exception ex)
            {
                // Mostra mensagem de erro no InfoBar
                ShowValidationError($"Erro no build: {ex.Message}");
                AppendTerminalLog($"Erro no build: {ex.Message}");
            }
            finally
            {
                BuildarDotnetButton.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }




        private void ProjetoDelphiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void ProcessarDelphiButton_Click(object sender, RoutedEventArgs e)
        {

        }
        private void Expander_IsExpandedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var expandedExpander = sender as Expander;

            if (expandedExpander.IsExpanded)
            {
                // Fecha o outro Expander
                if (expandedExpander == ExpanderDotNet)
                    ExpanderDelphi.IsExpanded = false;
                else if (expandedExpander == ExpanderDelphi)
                    ExpanderDotNet.IsExpanded = false;
            }
        }
        private void ShowValidationError(string mensagem)
        {
            ValidationInfoBar.Message = mensagem;
            ValidationInfoBar.IsOpen = true;
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
