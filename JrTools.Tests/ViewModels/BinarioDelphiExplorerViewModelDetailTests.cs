using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Testes de propriedade para detalhe e comandos do <see cref="BinarioDelphiExplorerViewModel"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class BinarioDelphiExplorerViewModelDetailTests
    {
        // -----------------------------------------------------------------------
        // Geradores auxiliares
        // -----------------------------------------------------------------------

        /// <summary>
        /// Gera um array de nomes de arquivo únicos (lowercase) com extensões .exe, .dll ou .bpl.
        /// </summary>
        private static Gen<string[]> GenNomesUnicos() =>
            Gen.NonEmptyListOf(
                    Arb.Generate<PositiveInt>()
                        .SelectMany(n =>
                            Gen.Elements(".exe", ".dll", ".bpl")
                               .Select(ext => $"file{n.Get}{ext}")))
               .Select(lista => lista.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        // -----------------------------------------------------------------------
        // Helpers de setup
        // -----------------------------------------------------------------------

        /// <summary>
        /// Cria arquivos temporários reais no disco e retorna seus FileInfo.
        /// </summary>
        private static (List<FileInfo> arquivos, string tempDir) CriarArquivosReais(string[] nomes)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "JrToolsDetailTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var arquivos = new List<FileInfo>();
            foreach (var nome in nomes)
            {
                var caminho = Path.Combine(tempDir, nome);
                File.WriteAllBytes(caminho, Array.Empty<byte>());
                arquivos.Add(new FileInfo(caminho));
            }

            return (arquivos, tempDir);
        }

        private static void LimparDiretorioTemp(string tempDir)
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* ignora erros de limpeza */ }
        }

        /// <summary>
        /// Cria um ViewModel com o índice já populado a partir dos nomes fornecidos,
        /// usando arquivos reais no disco (necessário para <see cref="BinarioDelphiIndexService.CarregarDetalheAsync"/>).
        /// </summary>
        private static (BinarioDelphiExplorerViewModel vm, BinarioDelphiIndexService indexService, List<FileInfo> arquivos, string tempDir) CriarViewModelComArquivosReais(
            string[] nomes,
            FakeProcessLauncher? processLauncher = null,
            FakeClipboardService? clipboardService = null)
        {
            var (arquivos, tempDir) = CriarArquivosReais(nomes);

            // Usar FakeDirectoryReader com os FileInfo reais (que existem no disco)
            var reader = new FakeDirectoryReader(arquivos);
            var indexService = new BinarioDelphiIndexService(reader);

            var indice = indexService.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

            var vm = new BinarioDelphiExplorerViewModel(indexService, processLauncher, clipboardService);
            vm.SetIndiceForTesting(indice);

            return (vm, indexService, arquivos, tempDir);
        }

        // -----------------------------------------------------------------------
        // Property 9: Detalhe carregado corresponde ao item selecionado
        // Validates: Requirements 7.1, 7.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 9: Detalhe carregado corresponde ao item selecionado
        [Property]
        public Property Property9_DetalheCorrespondeAoItemSelecionado()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (vm, indexService, arquivos, tempDir) = CriarViewModelComArquivosReais(nomes);
                    try
                    {
                        // Para cada item no índice, verificar que o detalhe carregado
                        // tem CaminhoCompleto igual ao do item.
                        // Chamamos o serviço diretamente pois o ViewModel usa dispatcher
                        // (não disponível em testes) para atualizar DetalheAtual.
                        foreach (var item in vm.ItensFiltrados.ToList())
                        {
                            var detalhe = indexService.CarregarDetalheAsync(item).GetAwaiter().GetResult();

                            if (detalhe.CaminhoCompleto != item.CaminhoCompleto)
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 14: "Abrir no Explorer" usa o caminho correto
        // Validates: Requirement 9.2
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 14: "Abrir no Explorer" usa o caminho correto
        [Property]
        public Property Property14_AbrirNoExplorerUsaCaminhoCorreto()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var fakeProcessLauncher = new FakeProcessLauncher();
                    var (vm, indexService, arquivos, tempDir) = CriarViewModelComArquivosReais(nomes, processLauncher: fakeProcessLauncher);
                    try
                    {
                        // Selecionar o primeiro item disponível
                        var item = vm.ItensFiltrados.First();
                        vm.ItemSelecionado = item;

                        // Executar o comando
                        vm.AbrirNoExplorerCommand.Execute(null);

                        // Verificar que o launcher foi chamado com os argumentos corretos
                        if (fakeProcessLauncher.LastFileName != "explorer.exe")
                            return false;

                        if (fakeProcessLauncher.LastArguments == null)
                            return false;

                        if (!fakeProcessLauncher.LastArguments.Contains(item.CaminhoCompleto))
                            return false;

                        return true;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 15: "Copiar Caminho" coloca o caminho correto na área de transferência
        // Validates: Requirement 10.2
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 15: "Copiar Caminho" coloca o caminho correto na área de transferência
        [Property]
        public Property Property15_CopiarCaminhoColocaCaminhoCorretoNaAreaDeTransferencia()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var fakeClipboard = new FakeClipboardService();
                    var (vm, indexService, arquivos, tempDir) = CriarViewModelComArquivosReais(nomes, clipboardService: fakeClipboard);
                    try
                    {
                        // Selecionar o primeiro item disponível
                        var item = vm.ItensFiltrados.First();
                        vm.ItemSelecionado = item;

                        // Executar o comando
                        vm.CopiarCaminhoCommand.Execute(null);

                        // Verificar que o clipboard recebeu exatamente o CaminhoCompleto do item
                        return fakeClipboard.LastText == item.CaminhoCompleto;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }
    }
}
