using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Services;
using JrTools.Tests.Helpers;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes de propriedade para <see cref="BinarioDelphiIndexService"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class BinarioDelphiIndexServiceTests
    {
        // -----------------------------------------------------------------------
        // Gerador auxiliar: produz um array de nomes de arquivo únicos (lowercase)
        // na forma "file{n}.exe|.dll|.bpl", garantindo unicidade pelo sufixo inteiro.
        // -----------------------------------------------------------------------
        private static Gen<string[]> GenNomesUnicos() =>
            Gen.NonEmptyListOf(
                    Arb.Generate<PositiveInt>()
                        .SelectMany(n =>
                            Gen.Elements(".exe", ".dll", ".bpl")
                               .Select(ext => $"file{n.Get}{ext}")))
               .Select(lista => lista.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

        /// <summary>
        /// Cria arquivos temporários reais no disco e retorna seus FileInfo.
        /// Os arquivos são criados vazios (0 bytes) apenas para satisfazer FileInfo.Length.
        /// </summary>
        private static (List<FileInfo> arquivos, string tempDir) CriarArquivosReais(string[] nomes)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "JrToolsTests_" + Guid.NewGuid().ToString("N"));
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

        // -----------------------------------------------------------------------
        // Property 1: Completude do índice
        // Validates: Requirements 3.1, 3.5
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 1: Completude do índice
        [Property]
        public Property Property1_CompletudeDoIndice()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (arquivos, tempDir) = CriarArquivosReais(nomes);
                    try
                    {
                        var reader = new FakeDirectoryReader(arquivos);
                        var servico = new BinarioDelphiIndexService(reader);

                        var indice = servico.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

                        return indice.Count == nomes.Length;
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 2: Idempotência da construção do índice
        // Validates: Requirement 3.3
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 2: Idempotência da construção do índice
        [Property]
        public Property Property2_IdempotenciaDaConstrucao()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (arquivos, tempDir) = CriarArquivosReais(nomes);
                    try
                    {
                        var reader = new FakeDirectoryReader(arquivos);
                        var servico = new BinarioDelphiIndexService(reader);

                        var indice1 = servico.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();
                        var indice2 = servico.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

                        return ReferenceEquals(indice1, indice2);
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 11: Lookup O(1) por nome normalizado
        // Validates: Requirement 12.4
        // -----------------------------------------------------------------------

        // Feature: binarios-delphi-explorer, Property 11: Lookup O(1) por nome normalizado
        [Property]
        public Property Property11_LookupPorNomeNormalizado()
        {
            return Prop.ForAll(
                GenNomesUnicos().ToArbitrary(),
                nomes =>
                {
                    var (arquivos, tempDir) = CriarArquivosReais(nomes);
                    try
                    {
                        var reader = new FakeDirectoryReader(arquivos);
                        var servico = new BinarioDelphiIndexService(reader);

                        var indice = servico.ConstruirIndiceAsync("fake_dir").GetAwaiter().GetResult();

                        // Para cada item no índice, o lookup pelo NomeNormalizado deve retornar
                        // o BinarioDelphiItem correto com Nome e CaminhoCompleto correspondentes.
                        foreach (var item in indice.Values)
                        {
                            if (!indice.TryGetValue(item.NomeNormalizado, out var encontrado))
                                return false;

                            if (encontrado.Nome != item.Nome)
                                return false;

                            if (encontrado.CaminhoCompleto != item.CaminhoCompleto)
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
    }
}
