using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Dto;
using JrTools.Services;
using JrTools.Tests.Helpers;
using JrTools.ViewModels;
using Xunit;

namespace JrTools.Tests.ViewModels
{
    /// <summary>
    /// Testes de propriedade e de exemplo para <see cref="ViewPathExplorerViewModel"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class ViewPathExplorerViewModelTests
    {
        // -----------------------------------------------------------------------
        // Property 1: Normalização elimina entradas inválidas
        // Feature: view-path-explorer, Property 1: Normalização elimina entradas inválidas
        // Validates: Requisitos 2.2, 2.3
        // -----------------------------------------------------------------------

        [Property]
        public Property Property1_NormalizacaoEliminaEntradasInvalidas()
        {
            return Prop.ForAll(
                Arb.Default.String(),
                texto =>
                {
                    var vm = new ViewPathExplorerViewModel(
                        new FakeViewPathMapper(),
                        new FakeClipboardService(),
                        new FakeConfigHelper());

                    var resultado = vm.NormalizarEntrada(texto ?? string.Empty);

                    // (a) Nenhum item vazio ou composto apenas de whitespace
                    var semVazios = resultado.All(item => !string.IsNullOrWhiteSpace(item));

                    // (b) Nenhum item com espaços no início ou no fim
                    var semEspacosBorda = resultado.All(item => item == item.Trim());

                    // (c) Sem duplicatas case-insensitive
                    var semDuplicatas = resultado.Count() ==
                        resultado.Distinct(System.StringComparer.OrdinalIgnoreCase).Count();

                    return semVazios && semEspacosBorda && semDuplicatas;
                });
        }

        // -----------------------------------------------------------------------
        // Exemplo 1: DiretorioEspecificos não configurado → MensagemErro preenchida
        // Validates: Requisito 1.2
        // -----------------------------------------------------------------------

        [Fact]
        public async Task InicializarAsync_DiretorioEspecificosNaoConfigurado_PreencheMensagemErro()
        {
            // Arrange: configHelper retorna DiretorioEspecificos vazio
            var fakeConfig = new FakeConfigHelper
            {
                ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                {
                    DiretorioBinarios = string.Empty,
                    DiretorioProducao = string.Empty,
                    DiretorioEspecificos = string.Empty
                }
            };

            var vm = new ViewPathExplorerViewModel(
                new FakeViewPathMapper(),
                new FakeClipboardService(),
                fakeConfig);

            // Act
            await vm.InicializarAsync();

            // Assert: MensagemErro deve estar preenchida (Req 1.2)
            Assert.False(string.IsNullOrWhiteSpace(vm.MensagemErro),
                "MensagemErro deve ser preenchida quando DiretorioEspecificos não está configurado.");
        }

        // -----------------------------------------------------------------------
        // Exemplo 2: ConfigHelper.SalvarConfiguracoesAsync chamado com ProjetoSelecionado correto
        // Validates: Requisito 8.1
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProjetoSelecionado_QuandoAlterado_SalvaProjetoCorretoViaConfigHelper()
        {
            // Arrange: diretório temporário com uma subpasta para simular um projeto disponível
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var nomeProjeto = "ProjetoTeste";
            var dirProjeto = Path.Combine(dirTemp, nomeProjeto);
            Directory.CreateDirectory(dirProjeto);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var vm = new ViewPathExplorerViewModel(
                    new FakeViewPathMapper(),
                    new FakeClipboardService(),
                    fakeConfig);

                // Inicializar para popular a lista de projetos
                await vm.InicializarAsync();

                var projeto = vm.Projetos.FirstOrDefault(p => p.Nome == nomeProjeto);
                Assert.NotNull(projeto); // pré-condição: projeto deve estar na lista

                // Act: selecionar o projeto (dispara fire-and-forget de salvamento)
                vm.ProjetoSelecionado = projeto;

                // Aguardar o fire-and-forget completar (yield para o scheduler de tasks)
                await Task.Yield();
                await Task.Delay(50); // margem para a task assíncrona de salvamento concluir

                // Assert: SalvarConfiguracoesAsync deve ter sido chamado com o nome correto (Req 8.1)
                Assert.True(fakeConfig.SalvarCallCount >= 1,
                    "SalvarConfiguracoesAsync deve ter sido chamado ao selecionar um projeto.");
                Assert.Equal(nomeProjeto, fakeConfig.UltimaConfiguracaoSalva?.ProjetoSelecionado);
            }
            finally
            {
                // Limpeza do diretório temporário
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Exemplo 3: Projeto salvo inexistente → ProjetoSelecionado null, sem erro
        // Validates: Requisito 8.3
        // -----------------------------------------------------------------------

        [Fact]
        public async Task InicializarAsync_ProjetoSalvoInexistente_ProjetoSelecionadoNullSemErro()
        {
            // Arrange: diretório temporário sem subpastas (nenhum projeto disponível),
            // mas com um nome de projeto salvo que não existe mais
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dirTemp);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp,
                        ProjetoSelecionado = "ProjetoQueNaoExisteMais"
                    }
                };

                var vm = new ViewPathExplorerViewModel(
                    new FakeViewPathMapper(),
                    new FakeClipboardService(),
                    fakeConfig);

                // Act
                await vm.InicializarAsync();

                // Assert: ProjetoSelecionado deve ser null (Req 8.3)
                Assert.Null(vm.ProjetoSelecionado);

                // Assert: nenhuma mensagem de erro deve ser exibida (Req 8.3)
                Assert.True(string.IsNullOrEmpty(vm.MensagemErro),
                    "Não deve haver mensagem de erro quando o projeto salvo não existe mais.");
            }
            finally
            {
                // Limpeza do diretório temporário
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Exemplo 4: BuscarCommand.CanExecute() = false quando nenhum projeto selecionado
        // Validates: Requisito 1.5
        // -----------------------------------------------------------------------

        [Fact]
        public void BuscarCommand_CanExecute_FalseQuandoNenhumProjetoSelecionado()
        {
            // Arrange
            var vm = new ViewPathExplorerViewModel(
                new FakeViewPathMapper(),
                new FakeClipboardService(),
                new FakeConfigHelper());

            vm.TextoViews = "IWFuncionarios";
            // ProjetoSelecionado permanece null

            // Act & Assert (Req 1.5)
            Assert.False(vm.BuscarCommand.CanExecute(null),
                "BuscarCommand deve estar desabilitado quando nenhum projeto está selecionado.");
        }

        // -----------------------------------------------------------------------
        // Exemplo 5: BuscarCommand.CanExecute() = false quando entrada é apenas whitespace
        // Validates: Requisito 2.2
        // -----------------------------------------------------------------------

        [Fact]
        public async Task BuscarCommand_CanExecute_FalseQuandoEntradaApenasWhitespace()
        {
            // Arrange: criar projeto temporário para poder selecioná-lo
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
            Directory.CreateDirectory(dirProjeto);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var vm = new ViewPathExplorerViewModel(
                    new FakeViewPathMapper(),
                    new FakeClipboardService(),
                    fakeConfig);

                await vm.InicializarAsync();
                vm.ProjetoSelecionado = vm.Projetos.First();
                vm.TextoViews = "   \n   \n   "; // apenas whitespace

                // Act & Assert (Req 2.2)
                Assert.False(vm.BuscarCommand.CanExecute(null),
                    "BuscarCommand deve estar desabilitado quando a entrada contém apenas whitespace.");
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Exemplo 6: BuscarCommand.CanExecute() = false durante IsCarregando = true
        // Validates: Requisito 3.6
        // -----------------------------------------------------------------------

        [Fact]
        public async Task BuscarCommand_CanExecute_FalseQuandoIsCarregandoTrue()
        {
            // Arrange: mapper que bloqueia para simular carregamento em andamento
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
            var dirPages = Path.Combine(dirProjeto, "Pages");
            Directory.CreateDirectory(dirPages);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                var blockingMapper = new BlockingFakeViewPathMapper(tcs.Task);

                var vm = new ViewPathExplorerViewModel(
                    blockingMapper,
                    new FakeClipboardService(),
                    fakeConfig);

                await vm.InicializarAsync();
                vm.ProjetoSelecionado = vm.Projetos.First();
                vm.TextoViews = "IWFuncionarios";

                // Iniciar busca (não aguardar — queremos verificar o estado durante o carregamento)
                var buscaTask = Task.Run(() => vm.BuscarCommand.Execute(null));
                await Task.Delay(50); // dar tempo para IsCarregando = true ser setado

                // Act & Assert (Req 3.6)
                Assert.False(vm.BuscarCommand.CanExecute(null),
                    "BuscarCommand deve estar desabilitado durante IsCarregando = true.");

                // Liberar o mapper bloqueado para não deixar tasks pendentes
                tcs.SetResult(true);
                await buscaTask;
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Exemplo 7: CopiarTudoCommand.CanExecute() = false quando Resultados está vazio
        // Validates: Requisito 6.3
        // -----------------------------------------------------------------------

        [Fact]
        public void CopiarTudoCommand_CanExecute_FalseQuandoResultadosVazio()
        {
            // Arrange
            var vm = new ViewPathExplorerViewModel(
                new FakeViewPathMapper(),
                new FakeClipboardService(),
                new FakeConfigHelper());

            // Resultados está vazio por padrão

            // Act & Assert (Req 6.3)
            Assert.False(vm.CopiarTudoCommand.CanExecute(null),
                "CopiarTudoCommand deve estar desabilitado quando não há resultados.");
        }

        // -----------------------------------------------------------------------
        // Exemplo 8: MensagemErro preenchida quando MapearCaminhosEmLote lança exceção
        // Validates: Requisito 7.2
        // -----------------------------------------------------------------------

        [Fact]
        public async Task BuscarCommand_QuandoMapperLancaExcecao_PreencheMensagemErro()
        {
            // Arrange
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
            var dirPages = Path.Combine(dirProjeto, "Pages");
            Directory.CreateDirectory(dirPages);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var throwingMapper = new ThrowingFakeViewPathMapper("Erro simulado de mapeamento");

                var vm = new ViewPathExplorerViewModel(
                    throwingMapper,
                    new FakeClipboardService(),
                    fakeConfig);

                await vm.InicializarAsync();
                vm.ProjetoSelecionado = vm.Projetos.First();
                vm.TextoViews = "IWFuncionarios";

                // Act: executar busca e aguardar conclusão
                vm.BuscarCommand.Execute(null);
                await Task.Delay(200); // aguardar a task assíncrona concluir

                // Assert (Req 7.2)
                Assert.False(string.IsNullOrWhiteSpace(vm.MensagemErro),
                    "MensagemErro deve ser preenchida quando MapearCaminhosEmLote lança exceção.");
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Exemplo 9: IsCarregando = false após conclusão da busca
        // Validates: Requisito 5.5
        // -----------------------------------------------------------------------

        [Fact]
        public async Task BuscarCommand_AposConclusao_IsCarregandoFalse()
        {
            // Arrange
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
            var dirPages = Path.Combine(dirProjeto, "Pages");
            Directory.CreateDirectory(dirPages);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var fakeMapper = new FakeViewPathMapper();
                fakeMapper.Respostas["IWFuncionarios"] = new System.Collections.Generic.List<string>
                {
                    "Funcionários > Grid [IWFuncionarios]"
                };

                var vm = new ViewPathExplorerViewModel(
                    fakeMapper,
                    new FakeClipboardService(),
                    fakeConfig);

                await vm.InicializarAsync();
                vm.ProjetoSelecionado = vm.Projetos.First();
                vm.TextoViews = "IWFuncionarios";

                // Act: executar busca e aguardar conclusão
                vm.BuscarCommand.Execute(null);
                await Task.Delay(200); // aguardar a task assíncrona concluir

                // Assert (Req 5.5)
                Assert.False(vm.IsCarregando,
                    "IsCarregando deve ser false após a conclusão da busca.");
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        [Fact]
        public async Task BuscarCommand_ProjetoComWesWebAppArtifacts_UsaArtifactsComoRaiz()
        {
            var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var dirProjeto = Path.Combine(dirTemp, "Prod");
            var dirArtifacts = Path.Combine(dirProjeto, "WES", "WebApp", "Artifacts");
            var dirPages = Path.Combine(dirArtifacts, "Pages");
            Directory.CreateDirectory(dirPages);

            try
            {
                var fakeConfig = new FakeConfigHelper
                {
                    ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                    {
                        DiretorioBinarios = string.Empty,
                        DiretorioProducao = string.Empty,
                        DiretorioEspecificos = dirTemp
                    }
                };

                var fakeMapper = new FakeViewPathMapper();
                fakeMapper.Respostas["IWFuncionarios"] = new System.Collections.Generic.List<string>
                {
                    "Funcionarios > Grid [IWFuncionarios]"
                };

                var vm = new ViewPathExplorerViewModel(
                    fakeMapper,
                    new FakeClipboardService(),
                    fakeConfig);

                await vm.InicializarAsync();
                vm.ProjetoSelecionado = vm.Projetos.First();
                vm.TextoViews = "IWFuncionarios";

                vm.BuscarCommand.Execute(null);
                await Task.Delay(200);

                Assert.Equal(dirArtifacts, fakeMapper.LastInitializedDirectory);
                Assert.Single(vm.Resultados);
                Assert.Null(vm.MensagemErro);
            }
            finally
            {
                if (Directory.Exists(dirTemp))
                    Directory.Delete(dirTemp, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Property 5: Texto copiado contém exatamente os caminhos exibidos
        // Feature: view-path-explorer, Property 5: Texto copiado contém exatamente os caminhos exibidos
        // Validates: Requisito 6.2
        // -----------------------------------------------------------------------

        [Property]
        public Property Property5_TextoCopiado_ContemExatamenteOsCaminhosExibidos()
        {
            // Gerador de lista de ViewResultadoItem com caminhos variados
            var genCaminhos = Gen.ListOf(Arb.Generate<NonEmptyString>()
                .Select(s => s.Get.Trim()))
                .Where(lista => lista.All(s => !string.IsNullOrWhiteSpace(s)));

            var genItem = Arb.Generate<NonEmptyString>()
                .Select(s => s.Get.Trim())
                .SelectMany(nomeView =>
                    genCaminhos.Select(caminhos => new ViewResultadoItem
                    {
                        NomeView = nomeView,
                        Caminhos = caminhos.ToList()
                    }));

            var genLista = Gen.ListOf(genItem);

            return Prop.ForAll(
                Arb.From(genLista),
                resultados =>
                {
                    var vm = new ViewPathExplorerViewModel(
                        new FakeViewPathMapper(),
                        new FakeClipboardService(),
                        new FakeConfigHelper());

                    var texto = vm.FormatarResultadosParaCopia(resultados);

                    // Cada caminho presente em item.Caminhos deve aparecer no texto formatado
                    foreach (var item in resultados)
                    {
                        foreach (var caminho in item.Caminhos)
                        {
                            if (!texto.Contains(caminho))
                                return false;
                        }
                    }

                    // Verificar que nenhum caminho extra foi adicionado:
                    // contar ocorrências de cada caminho no texto vs. na lista
                    var todosCaminhos = resultados
                        .SelectMany(r => r.Caminhos)
                        .ToList();

                    foreach (var caminho in todosCaminhos)
                    {
                        var ocorrenciasNoTexto = CountOccurrences(texto, caminho);
                        var ocorrenciasNaLista = todosCaminhos.Count(c =>
                            string.Equals(c, caminho, StringComparison.Ordinal));

                        if (ocorrenciasNoTexto < ocorrenciasNaLista)
                            return false;
                    }

                    return true;
                });
        }

        private static int CountOccurrences(string texto, string padrao)
        {
            if (string.IsNullOrEmpty(padrao)) return 0;
            int count = 0;
            int index = 0;
            while ((index = texto.IndexOf(padrao, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += padrao.Length;
            }
            return count;
        }

        // -----------------------------------------------------------------------
        // Property 3: Resultado contém entrada para cada view normalizada
        // Feature: view-path-explorer, Property 3: Resultado contém entrada para cada view normalizada
        // Validates: Requisito 4.3
        // -----------------------------------------------------------------------

        [Property]
        public Property Property3_Resultado_ContemEntradaParaCadaViewNormalizada()
        {
            // Gerador de conjunto de views normalizadas de tamanho variável (1..30)
            var genNonEmptyString = Arb.Generate<NonEmptyString>()
                .Select(s => s.Get.Trim())
                .Select(s => new string(s.Where(c => !char.IsControl(c)).ToArray()))
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var genConjuntoViews = Gen.Choose(1, 30)
                .SelectMany(size => Gen.ListOf(size, genNonEmptyString))
                .Select(lista => lista.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList())
                .Where(lista => lista.Count > 0);

            return Prop.ForAll(
                Arb.From(genConjuntoViews),
                conjuntoViews =>
                {
                    // Arrange: criar diretório temporário com pasta Pages
                    var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
                    var dirPages = Path.Combine(dirProjeto, "Pages");
                    Directory.CreateDirectory(dirPages);

                    try
                    {
                        var fakeConfig = new FakeConfigHelper
                        {
                            ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                            {
                                DiretorioBinarios = string.Empty,
                                DiretorioProducao = string.Empty,
                                DiretorioEspecificos = dirTemp
                            }
                        };

                        var fakeMapper = new FakeViewPathMapper();

                        // Configurar algumas respostas aleatórias (algumas views terão caminhos, outras não)
                        var random = new System.Random(conjuntoViews.GetHashCode());
                        foreach (var view in conjuntoViews)
                        {
                            if (random.Next(2) == 0) // 50% de chance de ter caminhos
                            {
                                fakeMapper.Respostas[view] = new System.Collections.Generic.List<string>
                                {
                                    $"Página Teste > Widget [{view}]"
                                };
                            }
                        }

                        var vm = new ViewPathExplorerViewModel(
                            fakeMapper,
                            new FakeClipboardService(),
                            fakeConfig);

                        vm.InicializarAsync().GetAwaiter().GetResult();
                        vm.ProjetoSelecionado = vm.Projetos.First();

                        // Configurar TextoViews com as views geradas (uma por linha)
                        vm.TextoViews = string.Join("\n", conjuntoViews);

                        // Act: executar busca
                        vm.BuscarCommand.Execute(null);

                        // Aguardar a task assíncrona concluir
                        Task.Delay(200).GetAwaiter().GetResult();

                        // Assert: Resultados.Count deve ser igual ao número de views normalizadas (Req 4.3)
                        if (vm.Resultados.Count != conjuntoViews.Count)
                            return false;

                        // Assert: cada view normalizada deve ter exatamente uma entrada em Resultados (Req 4.3)
                        foreach (var view in conjuntoViews)
                        {
                            var entradasParaView = vm.Resultados.Count(r =>
                                string.Equals(r.NomeView, view, System.StringComparison.OrdinalIgnoreCase));

                            if (entradasParaView != 1)
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        // Limpeza do diretório temporário
                        if (Directory.Exists(dirTemp))
                            Directory.Delete(dirTemp, recursive: true);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 4: Views não encontradas retornam lista vazia
        // Feature: view-path-explorer, Property 4: Views não encontradas retornam lista vazia (não ausência de chave)
        // Validates: Requisito 4.4
        // -----------------------------------------------------------------------

        [Property]
        public Property Property4_ViewsNaoEncontradas_RetornamListaVazia()
        {
            // Gerador de nomes de views aleatórios não presentes no índice do FakeViewPathMapper
            var genNonEmptyString = Arb.Generate<NonEmptyString>()
                .Select(s => s.Get.Trim())
                .Select(s => new string(s.Where(c => !char.IsControl(c)).ToArray()))
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var genListaViews = Gen.Choose(1, 20)
                .SelectMany(size => Gen.ListOf(size, genNonEmptyString))
                .Select(lista => lista.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList())
                .Where(lista => lista.Count > 0);

            return Prop.ForAll(
                Arb.From(genListaViews),
                listaViews =>
                {
                    // Arrange: criar diretório temporário com pasta Pages
                    var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
                    var dirPages = Path.Combine(dirProjeto, "Pages");
                    Directory.CreateDirectory(dirPages);

                    try
                    {
                        var fakeConfig = new FakeConfigHelper
                        {
                            ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                            {
                                DiretorioBinarios = string.Empty,
                                DiretorioProducao = string.Empty,
                                DiretorioEspecificos = dirTemp
                            }
                        };

                        // FakeViewPathMapper sem respostas configuradas — todas as views retornarão lista vazia
                        var fakeMapper = new FakeViewPathMapper();

                        var vm = new ViewPathExplorerViewModel(
                            fakeMapper,
                            new FakeClipboardService(),
                            fakeConfig);

                        vm.InicializarAsync().GetAwaiter().GetResult();
                        vm.ProjetoSelecionado = vm.Projetos.First();

                        // Configurar TextoViews com as views geradas (uma por linha)
                        vm.TextoViews = string.Join("\n", listaViews);

                        // Act: executar busca
                        vm.BuscarCommand.Execute(null);

                        // Aguardar a task assíncrona concluir
                        Task.Delay(200).GetAwaiter().GetResult();

                        // Assert: cada view não encontrada deve ter SemCaminhos == true e Caminhos.Count == 0 (Req 4.4)
                        foreach (var resultado in vm.Resultados)
                        {
                            if (!resultado.SemCaminhos)
                                return false;

                            if (resultado.Caminhos.Count != 0)
                                return false;
                        }

                        // Assert: todas as views devem estar presentes nos resultados (não ausência de chave)
                        if (vm.Resultados.Count != listaViews.Count)
                            return false;

                        return true;
                    }
                    finally
                    {
                        // Limpeza do diretório temporário
                        if (Directory.Exists(dirTemp))
                            Directory.Delete(dirTemp, recursive: true);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 2: EnsureInitialized é chamado exatamente uma vez por lote
        // Feature: view-path-explorer, Property 2: EnsureInitialized é chamado exatamente uma vez por lote
        // Validates: Requisitos 3.1, 4.2
        // -----------------------------------------------------------------------

        [Property]
        public Property Property2_EnsureInitialized_ChamadoExatamenteUmaVezPorLote()
        {
            // Gerador de lista de views não vazias (tamanho 1..50)
            var genNonEmptyString = Arb.Generate<NonEmptyString>()
                .Select(s => s.Get.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var genListaViews = Gen.Choose(1, 50)
                .SelectMany(size => Gen.ListOf(size, genNonEmptyString))
                .Select(lista => lista.ToList())
                .Where(lista => lista.Count > 0);

            return Prop.ForAll(
                Arb.From(genListaViews),
                listaViews =>
                {
                    // Arrange: criar diretório temporário com pasta Pages
                    var dirTemp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    var dirProjeto = Path.Combine(dirTemp, "ProjetoTeste");
                    var dirPages = Path.Combine(dirProjeto, "Pages");
                    Directory.CreateDirectory(dirPages);

                    try
                    {
                        var fakeConfig = new FakeConfigHelper
                        {
                            ConfiguracaoParaRetornar = new ConfiguracoesdataObject
                            {
                                DiretorioBinarios = string.Empty,
                                DiretorioProducao = string.Empty,
                                DiretorioEspecificos = dirTemp
                            }
                        };

                        var fakeMapper = new FakeViewPathMapper();

                        var vm = new ViewPathExplorerViewModel(
                            fakeMapper,
                            new FakeClipboardService(),
                            fakeConfig);

                        vm.InicializarAsync().GetAwaiter().GetResult();
                        vm.ProjetoSelecionado = vm.Projetos.First();

                        // Configurar TextoViews com as views geradas (uma por linha)
                        vm.TextoViews = string.Join("\n", listaViews);

                        // Act: executar busca
                        vm.BuscarCommand.Execute(null);

                        // Aguardar a task assíncrona concluir
                        Task.Delay(200).GetAwaiter().GetResult();

                        // Assert: EnsureInitialized deve ter sido chamado exatamente uma vez (Req 3.1, 4.2)
                        return fakeMapper.EnsureInitializedCallCount == 1;
                    }
                    finally
                    {
                        // Limpeza do diretório temporário
                        if (Directory.Exists(dirTemp))
                            Directory.Delete(dirTemp, recursive: true);
                    }
                });
        }
    }

    // ── Helpers de teste ──────────────────────────────────────────────────────

    /// <summary>
    /// Mapper fake que bloqueia MapearCaminhosEmLote até que a task fornecida complete.
    /// Usado para testar o estado IsCarregando durante a busca.
    /// </summary>
    internal class BlockingFakeViewPathMapper : IViewPathMapper
    {
        private readonly Task _bloqueio;

        public BlockingFakeViewPathMapper(Task bloqueio) => _bloqueio = bloqueio;

        public void EnsureInitialized(string diretorio) { }

        public Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes, string diretorio)
        {
            _bloqueio.Wait();
            return visoes.ToDictionary(v => v, _ => new List<string>());
        }
    }

    /// <summary>
    /// Mapper fake que lança exceção em MapearCaminhosEmLote.
    /// Usado para testar o tratamento de erros no ViewModel.
    /// </summary>
    internal class ThrowingFakeViewPathMapper : IViewPathMapper
    {
        private readonly string _mensagem;

        public ThrowingFakeViewPathMapper(string mensagem) => _mensagem = mensagem;

        public void EnsureInitialized(string diretorio) { }

        public Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes, string diretorio)
            => throw new InvalidOperationException(_mensagem);
    }
}
