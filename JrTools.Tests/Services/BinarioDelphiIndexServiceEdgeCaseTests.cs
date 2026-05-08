using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JrTools.Dto;
using JrTools.Services;
using JrTools.Tests.Helpers;
using JrTools.ViewModels;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes de exemplo (unit tests) para casos de borda do <see cref="BinarioDelphiIndexService"/>.
    /// </summary>
    public class BinarioDelphiIndexServiceEdgeCaseTests
    {
        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static string CriarDiretorioTemp()
        {
            var dir = Path.Combine(Path.GetTempPath(), "JrToolsEdge_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void LimparDiretorioTemp(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* ignora erros de limpeza */ }
        }

        // -----------------------------------------------------------------------
        // Test 1: DiretorioVazio_RetornaIndiceVazio
        // Validates: Requirement 2.2
        // -----------------------------------------------------------------------

        [Fact]
        public async void DiretorioVazio_RetornaIndiceVazio()
        {
            // Arrange
            var emptyDir = CriarDiretorioTemp();

            try
            {
                var service = new BinarioDelphiIndexService(); // usa DirectoryReaderImpl real

                // Act
                var indice = await service.ConstruirIndiceAsync(emptyDir);

                // Assert
                Assert.Empty(indice);
                Assert.True(service.IndiceDisponivel);
            }
            finally
            {
                LimparDiretorioTemp(emptyDir);
            }
        }

        // -----------------------------------------------------------------------
        // Test 2: DiretorioInexistente_RetornaIndiceVazio
        // Validates: Requirement 2.3
        // -----------------------------------------------------------------------

        [Fact]
        public async void DiretorioInexistente_RetornaIndiceVazio()
        {
            // Arrange — caminho que definitivamente não existe
            var nonExistentDir = Path.Combine(Path.GetTempPath(), "dir_que_nao_existe_" + Guid.NewGuid());

            var service = new BinarioDelphiIndexService(); // usa DirectoryReaderImpl real

            // Act — não deve lançar exceção
            var exception = await Record.ExceptionAsync(() => service.ConstruirIndiceAsync(nonExistentDir));

            var indice = await service.ConstruirIndiceAsync(nonExistentDir);

            // Assert
            Assert.Null(exception);
            Assert.Empty(indice);
        }

        // -----------------------------------------------------------------------
        // Test 3: DiretorioBinarios_Nulo_RetornaIndiceVazio
        // Validates: Requirement 2.2
        // -----------------------------------------------------------------------

        [Fact]
        public async void DiretorioBinarios_Nulo_RetornaIndiceVazio()
        {
            // Arrange — FakeDirectoryReader que retorna lista vazia independente do diretório
            var fakeReader = new FakeDirectoryReader(Enumerable.Empty<FileInfo>());
            var service = new BinarioDelphiIndexService(fakeReader);

            // Act — string.Empty não deve lançar exceção
            var exception = await Record.ExceptionAsync(() => service.ConstruirIndiceAsync(string.Empty));
            var indice = await service.ConstruirIndiceAsync(string.Empty);

            // Assert
            Assert.Null(exception);
            Assert.Empty(indice);
        }

        // -----------------------------------------------------------------------
        // Test 4: Performance_ConstrucaoIndice5000Arquivos_MenorQue3s
        // Validates: Requirement 12.1
        // -----------------------------------------------------------------------

        [Fact]
        public async void Performance_ConstrucaoIndice5000Arquivos_MenorQue3s()
        {
            // Arrange — criar 5000 arquivos reais em disco (zero bytes)
            var tempDir = CriarDiretorioTemp();

            try
            {
                var nomes = Enumerable.Range(0, 5000).Select(i => $"file{i}.exe").ToArray();

                // Criar arquivos reais no disco (necessário para FileInfo.Length)
                foreach (var nome in nomes)
                {
                    var caminho = Path.Combine(tempDir, nome);
                    File.WriteAllBytes(caminho, Array.Empty<byte>());
                }

                var arquivos = nomes.Select(n => new FileInfo(Path.Combine(tempDir, n))).ToList();
                var fakeReader = new FakeDirectoryReader(arquivos);
                var service = new BinarioDelphiIndexService(fakeReader);

                // Act
                var sw = Stopwatch.StartNew();
                var indice = await service.ConstruirIndiceAsync(tempDir);
                sw.Stop();

                // Assert
                Assert.Equal(5000, indice.Count);
                Assert.True(sw.ElapsedMilliseconds <= 3000,
                    $"Construção do índice levou {sw.ElapsedMilliseconds}ms, esperado ≤ 3000ms");
            }
            finally
            {
                LimparDiretorioTemp(tempDir);
            }
        }

        // -----------------------------------------------------------------------
        // Test 5: Performance_Filtro5000Itens_MenorQue100ms
        // Validates: Requirement 12.2
        // -----------------------------------------------------------------------

        [Fact]
        public async void Performance_Filtro5000Itens_MenorQue100ms()
        {
            // Arrange — criar 5000 arquivos reais em disco
            var tempDir = CriarDiretorioTemp();

            try
            {
                var nomes = Enumerable.Range(0, 5000).Select(i => $"file{i}.exe").ToArray();

                foreach (var nome in nomes)
                {
                    var caminho = Path.Combine(tempDir, nome);
                    File.WriteAllBytes(caminho, Array.Empty<byte>());
                }

                var arquivos = nomes.Select(n => new FileInfo(Path.Combine(tempDir, n))).ToList();
                var fakeReader = new FakeDirectoryReader(arquivos);
                var indexService = new BinarioDelphiIndexService(fakeReader);

                // Construir o índice primeiro
                var indice = await indexService.ConstruirIndiceAsync(tempDir);

                // Criar ViewModel e injetar o índice diretamente
                var vm = new BinarioDelphiExplorerViewModel(indexService);
                vm.SetIndiceForTesting(indice);

                // Act — medir tempo do filtro
                var sw = Stopwatch.StartNew();
                vm.TextoFiltroNome = "file1"; // corresponde a file1, file10..file19, file100..file199, etc. (~1111 itens)
                sw.Stop();

                // Assert
                Assert.True(sw.ElapsedMilliseconds <= 100,
                    $"Filtro levou {sw.ElapsedMilliseconds}ms, esperado ≤ 100ms");
                Assert.True(vm.ItensFiltrados.Count > 0,
                    "Filtro 'file1' deveria retornar itens");
            }
            finally
            {
                LimparDiretorioTemp(tempDir);
            }
        }
    }
}
