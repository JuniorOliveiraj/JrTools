using JrTools.Dto;
using JrTools.Enums;
using JrTools.Flows.Build;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class BuildarProjeto : Page
    {
        private ConfiguracoesdataObject _config;
        public List<PastaInformacoesDto> ListaDeProjetos { get; set; }
        public List<SolucaoInformacoesDto> ListaSolucoes { get; set; }

        private const int MAX_TERMINAL_LENGTH = 15000;
        public BuildarProjeto()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ExpanderDotNet.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, Expander_IsExpandedChanged);
            ExpanderDelphi.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, Expander_IsExpandedChanged);
            this.Loaded += ConfiguracoesPage_Loaded;
            AcaoBuildDotnetComboBox.SelectedIndex = 0;
            AcaoBuildDelphiComboBox.SelectedIndex = 0;
        }

        private async void ConfiguracoesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarConfiguracoes();
            CarregarMsBuildVersions();
            CarregarProjetos();
            CarregarProjetosDelphi();
        }

        private void CarregarMsBuildVersions()
        {
            var msBuildVersions = MsBuildLocator.FindMsBuildVersions();
            MsBuildVersionComboBox.ItemsSource = msBuildVersions;

            if (msBuildVersions.Any())
            {
                MsBuildInfo? versaoPadrao = null;
                if (!string.IsNullOrEmpty(_config?.MsBuildPadraoPath))
                {
                    versaoPadrao = msBuildVersions.FirstOrDefault(v => v.Path == _config.MsBuildPadraoPath);
                }

                MsBuildVersionComboBox.SelectedItem = versaoPadrao ?? msBuildVersions.First();
            }
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
            var direto = _config?.DiretorioEspecificos;
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
        private void CarregarProjetosDelphi()
        {
            var diretorio = _config?.DiretorioEspecificos;
            ListaDeProjetos = Folders.ListarPastas(diretorio);
            ProjetoDelphiselecionadoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoDelphiselecionadoComboBox.DisplayMemberPath = "Nome";

            var projetoDelphi = ListaDeProjetos.FirstOrDefault(p =>
                string.Equals(p.Nome, "prod", StringComparison.OrdinalIgnoreCase));

            if (projetoDelphi != null)
            {
                ProjetoDelphiselecionadoComboBox.SelectedItem = projetoDelphi;
            }
            else if (ListaDeProjetos.Any())
            {
                ProjetoDelphiselecionadoComboBox.SelectedIndex = 0;
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
            if (MsBuildVersionComboBox.SelectedItem is not MsBuildInfo msBuildInfo)
            {
                ShowValidationError("Selecione uma versão do MSBuild.");
                return;
            }

            if (SolucaoDotnetSelecionadoComboBox.SelectedItem is not SolucaoInformacoesDto solucaoSelecionada)
            {
                ShowValidationError("Selecione uma solução antes de processar.");
                return;
            }

            var acaoSelecionada = (AcaoBuild)AcaoBuildDotnetComboBox.SelectedIndex;

            BuildarDotnetButton.IsEnabled = false;
            LoadingRing.IsActive = true;

            string[] processos = { "BPrv230", "CS1" };
            var progresso = (IProgress<string>)new Progress<string>(msg => AppendTerminalLog(msg));
 
            // Verifica previamente se existe algum dos processos problemáticos em execução.
            // Só se houver, ativamos o "guardian" que mata e mantém esses processos fechados.
            bool precisaMonitorarProcessos = false;
            foreach (var nomeProcesso in processos)
            {
                try
                {
                    int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso);
                    if (qtd > 0)
                    {
                        precisaMonitorarProcessos = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    progresso.Report($"[WARN] Falha ao verificar processo {nomeProcesso}: {ex.Message}");
                }
            }

            try
            {
                // Se houver processos em execução, usamos o ProcessGuardianService para garantir
                // que eles permaneçam fechados durante todo o build.
                if (precisaMonitorarProcessos)
                {
                    var guardian = new ProcessGuardianService();
                    await guardian.ExecutarComProcessosFechadosAsync(
                        processos,
                        async _ => await ExecutarBuildDotnetAsync(solucaoSelecionada, msBuildInfo, acaoSelecionada, progresso),
                        progresso);
                }
                else
                {
                    // Se não há processos, apenas executa o build normalmente (sem matar nada em loop).
                    await ExecutarBuildDotnetAsync(solucaoSelecionada, msBuildInfo, acaoSelecionada, progresso);
                }
            }
            catch (Exception ex)
            {
                progresso.Report($"[ERRO] Falha inesperada: {ex.Message}");
            }
            finally
            {
                BuildarDotnetButton.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }

        private async Task ExecutarBuildDotnetAsync(SolucaoInformacoesDto solucaoSelecionada, MsBuildInfo msBuildInfo, AcaoBuild acaoSelecionada, IProgress<string> progresso)
        {
            AppendTerminalLog($"Iniciando {acaoSelecionada.ToString().ToLower()} da solução: {solucaoSelecionada.Nome}");

            try
            {
                var buildHandler = new BinldarProjetoSrv();
                await buildHandler.BuildarProjetoAsync(solucaoSelecionada.Caminho, msBuildInfo.Path, acaoSelecionada, progresso);
                AppendTerminalLog($"{acaoSelecionada.ToString()} concluído com sucesso!");

                if (acaoSelecionada == AcaoBuild.Build || acaoSelecionada == AcaoBuild.Rebuild)
                {
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
                        progresso.Report($"[WARN] Não foi possível abrir o navegador: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                progresso.Report($"[ERRO] {acaoSelecionada.ToString()} falhou: {ex.Message}");
                throw;
            }
        }

        private void ProjetoDelphiComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoDelphiselecionadoComboBox.SelectedItem is PastaInformacoesDto projetoSelecionado)
            {
                string caminhoProjeto = Path.Combine(projetoSelecionado.Caminho, "Delphi");

                ListaSolucoes = Folders.EncontrarProjetosDelphi(caminhoProjeto, usarCache: true);

                SolucaoDelphiSelecionadoComboBox.ItemsSource = ListaSolucoes;
                SolucaoDelphiSelecionadoComboBox.DisplayMemberPath = "Nome";

                SolucaoDelphiSelecionadoComboBox.SelectedIndex = ListaSolucoes.Any() ? 0 : -1;
            }
        }

        private async void ProcessarDelphiButton_Click(object sender, RoutedEventArgs e)
        {
            if (SolucaoDelphiSelecionadoComboBox.SelectedItem is not SolucaoInformacoesDto solucaoSelecionada)
            {
                ShowValidationError("Selecione uma solução Delphi antes de processar.");
                return;
            }
            
            if (MsBuildVersionComboBox.SelectedItem is not MsBuildInfo msBuildInfo)
            {
                ShowValidationError("Selecione uma versão do MSBuild.");
                return;
            }

            var acaoSelecionada = (AcaoBuild)AcaoBuildDelphiComboBox.SelectedIndex;

            BuildarDelphiButton.IsEnabled = false;
            LoadingDelphiRing.IsActive = true;

            var progresso = (IProgress<string>)new Progress<string>(msg => AppendTerminalLogDelphi(msg));

            try
            {
                // Localiza o rsvars.bat do Delphi de forma semelhante a como buscamos o MsBuildPadraoPath,
                // porém restrito à tela de build Delphi (não fica na tela de configurações geral).
                var rsvarsBatPath = ObterRsvarsBatPadrao();
                if (string.IsNullOrWhiteSpace(rsvarsBatPath))
                {
                    ShowValidationError("Arquivo rsvars.bat do Delphi não encontrado em caminhos padrão. Ajuste o método ObterRsvarsBatPadrao.");
                    return;
                }

                var buildHandler = new BuildarDelphiSrv();
                await buildHandler.BuildarAsync(solucaoSelecionada.Caminho, msBuildInfo.Path, rsvarsBatPath, acaoSelecionada, progresso);
            }
            catch (Exception ex)
            {
                progresso.Report($"[ERRO] Falha inesperada: {ex.Message}");
            }
            finally
            {
                BuildarDelphiButton.IsEnabled = true;
                LoadingDelphiRing.IsActive = false;
            }
        }

        /// <summary>
        /// Resolve o caminho padrão do rsvars.bat do Delphi.
        /// Similar em espírito ao carregamento do MsBuildPadraoPath, mas
        /// específico desta tela de build Delphi (não exposto na tela de configurações).
        /// </summary>
        /// <returns>Caminho completo do rsvars.bat, ou null se não encontrado.</returns>
        private string? ObterRsvarsBatPadrao()
        {
            // Adicione aqui outros caminhos que você utiliza no ambiente, se necessário.
            var caminhosPossiveis = new[]
            {
                @"C:\Program Files (x86)\Embarcadero\Studio\17.0\bin\rsvars.bat",
                @"C:\Program Files (x86)\Embarcadero\Studio\18.0\bin\rsvars.bat"
            };

            foreach (var caminho in caminhosPossiveis)
            {
                try
                {
                    if (File.Exists(caminho))
                        return caminho;
                }
                catch
                {
                    // Ignora erros de IO pontuais e continua tentando os próximos caminhos.
                }
            }

            return null;
        }

        private void Expander_IsExpandedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var expandedExpander = sender as Expander;

            if (expandedExpander.IsExpanded)
            {
                if (expandedExpander == ExpanderDotNet)
                    ExpanderDelphi.IsExpanded = false;
                else if (expandedExpander == ExpanderDelphi)
                    ExpanderDotNet.IsExpanded = false;
            }
        }

        private async Task GarantirProcessosFechados(string[] processos, IProgress<string> progresso)
        {
            foreach (var nomeProcesso in processos)
            {
                int tentativas = 0;
                while (await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso) > 0)
                {
                    progresso.Report($"Processo {nomeProcesso} ainda ativo, tentando fechar...");
                    await ProcessKiller.KillByNameAsync(nomeProcesso, progresso);
                    tentativas++;
                    if (tentativas > 10)
                        throw new Exception($"Não foi possível encerrar o processo {nomeProcesso} antes do build.");
                    await Task.Delay(1000);
                }
            }
        }

        private void ShowValidationError(string mensagem)
        {
            ValidationInfoBar.Message = mensagem;
            ValidationInfoBar.IsOpen = true;
        }

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

        private void AppendTerminalLogDelphi(string mensagem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TerminalDelphiOutput.Text += mensagem + "\n";

                if (TerminalDelphiOutput.Text.Length > MAX_TERMINAL_LENGTH)
                {
                    TerminalDelphiOutput.Text = TerminalDelphiOutput.Text.Substring(TerminalDelphiOutput.Text.Length - MAX_TERMINAL_LENGTH);
                }

                TerminalScrollViewerDelphi.ChangeView(null, TerminalScrollViewerDelphi.ScrollableHeight, null);
            });
        }
    }
}
