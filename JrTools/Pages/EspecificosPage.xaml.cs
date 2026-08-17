using JrTools.Dto;
using JrTools.Enums;
using JrTools.Flows.Build;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JrTools.Pages
{
    public sealed partial class EspecificosPage : Page
    {
        private ConfiguracoesdataObject _config;
        private ConfiguracaoRelatoriosRh _cfgRh;
        public List<PastaInformacoesDto> ListaDeProjetos { get; set; }
        private const int MAX_TERMINAL_LENGTH = 15000;
        private readonly IisService _iis = new();

        public EspecificosPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += ConfiguracoesPage_Loaded;
        }

        private async void ConfiguracoesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarConfiguracoes();
            CarregarProjetos();
            await Task.WhenAll(CarregarSitesAsync(), CarregarPoolsAsync());
        }

        private async Task CarregarConfiguracoes()
        {
            try
            {
                _config = await ConfigHelper.LerConfiguracoesAsync();
                _cfgRh = await ConfiguracaoRelatoriosHelper.LerAsync();

                if (_cfgRh != null)
                {
                    if (!string.IsNullOrWhiteSpace(_cfgRh.Servidor))
                        EnderecoServidorTextBox.Text = _cfgRh.Servidor;
                    if (!string.IsNullOrWhiteSpace(_cfgRh.Usuario))
                        UsuarioInternoTextBox.Text = _cfgRh.Usuario;
                    if (!string.IsNullOrWhiteSpace(_cfgRh.Senha))
                        SenhaInternoPasswordBox.Password = _cfgRh.Senha;
                }
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"[AVISO] Erro ao carregar configurações salvas: {ex.Message}");
                _config ??= new ConfiguracoesdataObject();
            }
        }

        private void CarregarProjetos()
        {
            var diretorio = _config?.DiretorioEspecificos;
            if (string.IsNullOrWhiteSpace(diretorio) || !Directory.Exists(diretorio))
            {
                diretorio = @"D:\Benner\fontes\rh";
            }

            ListaDeProjetos = Folders.ListarPastas(diretorio);
            ProjetoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoComboBox.DisplayMemberPath = "Nome";

            if (ListaDeProjetos.Any())
            {
                ProjetoComboBox.SelectedIndex = 0;
            }
        }

        private async Task CarregarSitesAsync()
        {
            try
            {
                var sites = await _iis.ListarSitesAsync();
                SiteComboBox.ItemsSource = sites;

                if (sites.Contains("Default Web Site", StringComparer.OrdinalIgnoreCase))
                {
                    SiteComboBox.SelectedItem = sites.FirstOrDefault(s => s.Equals("Default Web Site", StringComparison.OrdinalIgnoreCase));
                }
                else if (sites.Any())
                {
                    SiteComboBox.SelectedIndex = 0;
                }
                else
                {
                    SiteComboBox.Text = "Default Web Site";
                }
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"[AVISO] Não foi possível listar sites do IIS: {ex.Message}");
                SiteComboBox.Text = "Default Web Site";
            }
        }

        private async Task CarregarPoolsAsync()
        {
            try
            {
                var pools = await _iis.ListarPoolsAsync();
                PoolComboBox.ItemsSource = pools;

                if (pools.Contains("RHPool", StringComparer.OrdinalIgnoreCase))
                {
                    PoolComboBox.SelectedItem = pools.FirstOrDefault(p => p.Equals("RHPool", StringComparison.OrdinalIgnoreCase));
                }
                else if (pools.Any())
                {
                    PoolComboBox.SelectedIndex = 0;
                }
                else
                {
                    PoolComboBox.Text = "RHPool";
                }
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"[AVISO] Não foi possível listar pools do IIS: {ex.Message}");
                PoolComboBox.Text = "RHPool";
            }
        }

        private void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoComboBox.SelectedItem is PastaInformacoesDto projeto)
            {
                string nomeLimpo = projeto.Nome.ToUpper().Replace(" ", "_");
                NomeAplicacaoTextBox.Text = $"ESPRH_{nomeLimpo}_DESENV";
                NomeSistemaBennerTextBox.Text = $"ESPRH_{nomeLimpo}_DESENV";
            }
        }

        private async void ProcessarButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationInfoBar.IsOpen = false;

            var projetoSelecionado = ProjetoComboBox.SelectedItem as PastaInformacoesDto;
            string siteSelecionado = SiteComboBox.SelectedItem?.ToString() ?? SiteComboBox.Text?.Trim();
            string poolSelecionada = PoolComboBox.SelectedItem?.ToString() ?? PoolComboBox.Text?.Trim();

            if (projetoSelecionado == null ||
                string.IsNullOrWhiteSpace(EnderecoServidorTextBox.Text) ||
                string.IsNullOrWhiteSpace(UsuarioInternoTextBox.Text) ||
                string.IsNullOrWhiteSpace(SenhaInternoPasswordBox.Password) ||
                string.IsNullOrWhiteSpace(siteSelecionado) ||
                string.IsNullOrWhiteSpace(NomeAplicacaoTextBox.Text) ||
                string.IsNullOrWhiteSpace(poolSelecionada) ||
                string.IsNullOrWhiteSpace(NumeroProvedoresTextBox.Text) ||
                string.IsNullOrWhiteSpace(NomeSistemaBennerTextBox.Text))
            {
                ValidationInfoBar.Title = "Campos Obrigatórios";
                ValidationInfoBar.Message = "Por favor, selecione o projeto e preencha todos os campos antes de processar.";
                ValidationInfoBar.IsOpen = true;
                return;
            }

            var config = new PageEspecificosDataObject
            {
                Projeto = projetoSelecionado.Nome,
                BaixarBinario = BaixarBinarioToggle.IsOn,
                CriarAtalho = CriarAtalhoToggle.IsOn,
                CompilarEspecificos = CompilarEspecificosToggle.IsOn,
                CriarAplicacaoIIS = CriarAplicacaoIISToggle.IsOn,
                RestaurarWebApp = RestaurarWebAppToggle.IsOn,
                InstalarArtefatos = InstalarArtefatosToggle.IsOn,
                EnderecoServidor = EnderecoServidorTextBox.Text.Trim(),
                UsuarioInterno = UsuarioInternoTextBox.Text.Trim(),
                SenhaInterno = SenhaInternoPasswordBox.Password,
                Site = siteSelecionado,
                NomeAplicacao = NomeAplicacaoTextBox.Text.Trim(),
                Pool = poolSelecionada,
                NumeroProvedores = NumeroProvedoresTextBox.Text.Trim(),
                NomeSistemaBenner = NomeSistemaBennerTextBox.Text.Trim()
            };

            // Salva credenciais para futuras execuções
            if (_cfgRh != null)
            {
                _cfgRh.Servidor = config.EnderecoServidor;
                _cfgRh.Usuario = config.UsuarioInterno;
                _cfgRh.Senha = config.SenhaInterno;
                await ConfiguracaoRelatoriosHelper.SalvarAsync(_cfgRh);
            }

            ProcessarButton.IsEnabled = false;
            LoadingRing.IsActive = true;
            AppendTerminalLog($"\n=======================================================");
            AppendTerminalLog($"[INÍCIO] Montagem de Ambiente Específico - {projetoSelecionado.Nome}");
            AppendTerminalLog($"=======================================================");

            var progresso = new Progress<string>(msg => AppendTerminalLog(msg));

            try
            {
                // ETAPA 1: Baixar binário atualizado
                if (config.BaixarBinario)
                {
                    AppendTerminalLog("\n[ETAPA 1/5] Baixando/extraindo binário atualizado...");
                    var cfgBin = await ConfigHelper.LerConfiguracoesAsync();
                    IBinarioSourceProvider provider;
                    if (cfgBin.FonteBinarios == FonteBinarios.Jenkins)
                    {
                        var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();
                        provider = new JenkinsBinarioProvider(cfgBin.JenkinsBaseUrl, cfgBin.JenkinsJobPath, dados.JenkinsUsuario, dados.JenkinsApiToken);
                    }
                    else
                    {
                        provider = new ServidorBinarioProvider(cfgBin.CaminhoServidorBinarios);
                    }

                    var branchNorm = "prd-09.00";
                    if (!string.IsNullOrWhiteSpace(_config?.UltimaBranchAmbiente))
                    {
                        branchNorm = new JrTools.Utils.BranchNameHelper()
                            .ObterBranchInfo(_config.UltimaBranchAmbiente).Branch
                            .Replace("/", "-");
                    }

                    var binInfo = await provider.ObterBinarioAsync(branchNorm, progresso);
                    if (binInfo != null)
                    {
                        binInfo.destino = @"D:\Benner\bin";
                        var svcBin = new BinarioService();
                        await svcBin.ExtrairBinarioAsync(binInfo, progresso);
                    }
                    else
                    {
                        AppendTerminalLog($"[AVISO] Binário para '{branchNorm}' não encontrado. Pulando extração.");
                    }
                }

                // ETAPA 2: Criar atalho / junction link para pasta de compilação
                if (config.CriarAtalho)
                {
                    AppendTerminalLog("\n[ETAPA 2/5] Criando atalho (link simbólico) para pasta de compilação...");
                    await Task.Run(() => CriarLink(config.NomeAplicacao, progresso));
                }

                // ETAPA 3: Compilar Específicos
                if (config.CompilarEspecificos)
                {
                    AppendTerminalLog("\n[ETAPA 3/5] Compilando projetos específicos...");
                    var solucoes = Folders.ListarArquivosSln(projetoSelecionado.Caminho);

                    string msbuildPath = _config?.MsBuildPadraoPath;
                    if (string.IsNullOrEmpty(msbuildPath) || !File.Exists(msbuildPath))
                    {
                        var versao = MsBuildLocator.FindMsBuildVersions().FirstOrDefault();
                        msbuildPath = versao?.Path;
                    }

                    if (string.IsNullOrEmpty(msbuildPath) || !File.Exists(msbuildPath))
                    {
                        throw new FileNotFoundException("Executável do MSBuild não encontrado no sistema.");
                    }

                    if (solucoes.Any())
                    {
                        var buildSrv = new BinldarProjetoSrv();
                        foreach (var sln in solucoes)
                        {
                            AppendTerminalLog($"[BUILD] Compilando solução .NET: {sln.Nome}");
                            await buildSrv.BuildarProjetoAsync(sln.Caminho, msbuildPath, AcaoBuild.Build, progresso);
                        }
                    }

                    // Se houver projetos Delphi
                    string pastaDelphi = Path.Combine(projetoSelecionado.Caminho, "Delphi");
                    if (Directory.Exists(pastaDelphi))
                    {
                        var projetosDelphi = Folders.EncontrarProjetosDelphi(pastaDelphi);
                        var rsvarsBat = @"C:\Program Files (x86)\Embarcadero\Studio\17.0\bin\rsvars.bat";
                        if (!File.Exists(rsvarsBat))
                            rsvarsBat = @"C:\Program Files (x86)\Embarcadero\Studio\18.0\bin\rsvars.bat";

                        if (File.Exists(rsvarsBat) && projetosDelphi.Any())
                        {
                            var buildDelphiSrv = new BuildarDelphiSrv();
                            foreach (var projD in projetosDelphi)
                            {
                                AppendTerminalLog($"[BUILD DELPHI] Compilando: {projD.Nome}");
                                await buildDelphiSrv.BuildarAsync(projD.Caminho, msbuildPath, rsvarsBat, AcaoBuild.Build, progresso);
                            }
                        }
                    }
                }

                // Garante que os arquivos do WebApp do prod estejam vinculados no projeto específico
                var webAppLinker = new WebAppLinkService();
                await webAppLinker.GarantirLinkWebAppProdAsync(projetoSelecionado.Caminho, _config?.DiretorioProducao, progresso);

                // ETAPA 4: Criar aplicação no IIS
                if (config.CriarAplicacaoIIS)
                {
                    AppendTerminalLog("\n[ETAPA 4/5] Criando aplicação no IIS...");
                    string caminhoFisicoWes = Path.Combine(projetoSelecionado.Caminho, @"WES\WebApp");

                    if (!Directory.Exists(caminhoFisicoWes))
                    {
                        Directory.CreateDirectory(caminhoFisicoWes);
                    }

                    await _iis.CriarAplicacaoAsync(config.Site, config.NomeAplicacao, config.Pool, caminhoFisicoWes, progresso);
                }

                // ETAPA 5: Restaurar / Configurar WebApp no WES
                if (config.RestaurarWebApp)
                {
                    AppendTerminalLog("\n[ETAPA 5/5] Restaurando e configurando WebApp via WES...");
                    string wesExePath = Path.Combine(projetoSelecionado.Caminho, @"WES\WebApp\Bin\wes.exe");
                    string webConfigPath = Path.Combine(projetoSelecionado.Caminho, @"WES\WebApp\web.config");

                    if (!File.Exists(wesExePath))
                    {
                        // Fallback para WesExePath das configurações gerais se não houver bin local
                        wesExePath = _config?.WesExePath ?? @"D:\Benner\fontes\rh\prod\WES\WebApp\Bin\wes.exe";
                    }

                    if (!File.Exists(wesExePath))
                    {
                        throw new FileNotFoundException($"wes.exe não encontrado em: {wesExePath}");
                    }

                    var wes = new WesService(wesExePath);
                    await wes.ConfigSetAsync(config.EnderecoServidor, config.NomeSistemaBenner, config.UsuarioInterno, config.SenhaInterno, progresso);

                    if (File.Exists(webConfigPath))
                    {
                        InjetarUseCOMFree(webConfigPath, progresso);
                    }

                    await wes.CacheClearAsync(progresso);

                    if (config.InstalarArtefatos)
                    {
                        AppendTerminalLog("[WES] Instalação de artefatos habilitada. Instalando artefatos...");
                        await wes.ArtifactsInstallAsync(progresso);
                    }
                    else
                    {
                        AppendTerminalLog("[WES] Instalação automática de artefatos mantida desligada (a cargo do usuário).");
                    }

                    await wes.PagesGenerateAsync(progresso);
                }

                AppendTerminalLog("\n=======================================================");
                AppendTerminalLog("✓ [SUCESSO] Processo de montagem de ambiente concluído!");
                AppendTerminalLog("=======================================================");
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"\n[ERRO FATAL] {ex.Message}");
                ValidationInfoBar.Title = "Erro na Montagem de Ambiente";
                ValidationInfoBar.Message = ex.Message;
                ValidationInfoBar.Severity = InfoBarSeverity.Error;
                ValidationInfoBar.IsOpen = true;
            }
            finally
            {
                LoadingRing.IsActive = false;
                ProcessarButton.IsEnabled = true;
            }
        }

        private void AbrirNavegadorButton_Click(object sender, RoutedEventArgs e)
        {
            string nomeApp = NomeAplicacaoTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(nomeApp))
            {
                ValidationInfoBar.Title = "Nome da Aplicação";
                ValidationInfoBar.Message = "Informe o nome da aplicação antes de abrir no navegador.";
                ValidationInfoBar.IsOpen = true;
                return;
            }

            try
            {
                var url = $"http://localhost/{nomeApp}";
                AppendTerminalLog($"[NAVEGADOR] Abrindo {url}...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppendTerminalLog($"[ERRO] Não foi possível abrir o navegador: {ex.Message}");
            }
        }

        private void CriarLink(string nomeAplicacao, IProgress<string> progresso)
        {
            string destino = $@"D:\Benner\bin\{nomeAplicacao}";
            string origem = @"D:\Benner\bin\Delphi";

            if (!Directory.Exists(@"D:\Benner\bin"))
                Directory.CreateDirectory(@"D:\Benner\bin");

            if (!Directory.Exists(origem))
                Directory.CreateDirectory(origem);

            if (Directory.Exists(destino))
            {
                progresso.Report($"[LINK] Destino já existe: {destino}. Removendo antes de recriar...");
                try
                {
                    Directory.Delete(destino, recursive: false);
                }
                catch (Exception ex)
                {
                    progresso.Report($"[LINK WARN] Não foi possível deletar pasta existente: {ex.Message}");
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{destino}\" \"{origem}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output)) progresso.Report($"[LINK] {output.Trim()}");
                if (!string.IsNullOrWhiteSpace(error)) progresso.Report($"[LINK ERRO] {error.Trim()}");

                progresso.Report(proc.ExitCode == 0
                    ? $"[LINK] Junction criado com sucesso: {destino} -> {origem}"
                    : $"[LINK ERRO] Falha ao criar junction link. Código: {proc.ExitCode}");
            }
        }

        private void InjetarUseCOMFree(string webConfigPath, IProgress<string> progresso)
        {
            const string KEY = "useCOMFree";
            try
            {
                var doc = XDocument.Load(webConfigPath);
                var appSettings = doc.Root?.Element("appSettings");
                if (appSettings == null)
                {
                    appSettings = new XElement("appSettings");
                    doc.Root!.Add(appSettings);
                }

                var existente = appSettings.Elements("add")
                    .FirstOrDefault(el => el.Attribute("key")?.Value == KEY);

                if (existente != null)
                {
                    progresso.Report($"[WEB.CONFIG] Chave '{KEY}' já presente em web.config.");
                    return;
                }

                appSettings.Add(new XElement("add",
                    new XAttribute("key", KEY),
                    new XAttribute("value", "false")));

                doc.Save(webConfigPath);
                progresso.Report($"[WEB.CONFIG] Chave '{KEY}' adicionada ao web.config.");
            }
            catch (Exception ex)
            {
                progresso.Report($"[WEB.CONFIG ERRO] Falha ao injetar '{KEY}': {ex.Message}");
            }
        }

        private void BtnLimparLog_Click(object sender, RoutedEventArgs e)
        {
            TerminalOutput.Text = "Terminal limpo.";
        }

        private void AppendTerminalLog(string mensagem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TerminalOutput.Text += mensagem + "\n";

                if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
                    TerminalOutput.Text = TerminalOutput.Text.Substring(TerminalOutput.Text.Length - MAX_TERMINAL_LENGTH);

                TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
            });
        }
    }
}
