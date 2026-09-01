using System;
using System.IO;
using JrTools.Services;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes para <see cref="RelatorioImportService.CalcularHash"/> — desde que os .rpt
    /// deixaram de ser exportados como blob binário criptografado e passaram a ser texto
    /// legível (pipe-delimitado), campos como HANDLE/ALTERADOPOR/ULTIMAALTERACAO mudam a
    /// cada exportação mesmo sem alteração real, então o hash de comparação precisa
    /// ignorá-los para não marcar tudo como "Diferente" o tempo todo.
    /// </summary>
    public class RelatorioImportServiceTests
    {
        private readonly RelatorioImportService _service = new();

        [Fact]
        public void CalcularHash_QuandoSoCamposVolateisMudam_RetornaOMesmoHash()
        {
            const string original = "@NEWFILE\n005\n[RELATORIOS]\n\t[RELATORIO]\n\t\t[ATRIBUTOS]\n\t\t\tHANDLE|1633\n\t\t\tCODIGO|P.1070\n\t\t\tNOME|Descricao\n\t\t\tALTERADOPOR|ingredy.anhaia\n\t\t\tULTIMAALTERACAO|17/09/2019 04:29\n";
            const string reexportado = "@NEWFILE\n005\n[RELATORIOS]\n\t[RELATORIO]\n\t\t[ATRIBUTOS]\n\t\t\tHANDLE|9999\n\t\t\tCODIGO|P.1070\n\t\t\tNOME|Descricao\n\t\t\tALTERADOPOR|outra.pessoa\n\t\t\tULTIMAALTERACAO|01/01/2026 10:00\n";

            var caminhoOriginal = CriarArquivoTemporario(original);
            var caminhoReexportado = CriarArquivoTemporario(reexportado);
            try
            {
                Assert.Equal(_service.CalcularHash(caminhoOriginal), _service.CalcularHash(caminhoReexportado));
            }
            finally
            {
                File.Delete(caminhoOriginal);
                File.Delete(caminhoReexportado);
            }
        }

        [Fact]
        public void CalcularHash_QuandoConteudoRelevanteMuda_RetornaHashDiferente()
        {
            const string original = "@NEWFILE\n005\n[RELATORIOS]\n\t[RELATORIO]\n\t\t[ATRIBUTOS]\n\t\t\tHANDLE|1633\n\t\t\tCODIGO|P.1070\n\t\t\tNOME|Descricao\n";
            const string modificado = "@NEWFILE\n005\n[RELATORIOS]\n\t[RELATORIO]\n\t\t[ATRIBUTOS]\n\t\t\tHANDLE|1633\n\t\t\tCODIGO|P.1070\n\t\t\tNOME|Descricao Nova\n";

            var caminhoOriginal = CriarArquivoTemporario(original);
            var caminhoModificado = CriarArquivoTemporario(modificado);
            try
            {
                Assert.NotEqual(_service.CalcularHash(caminhoOriginal), _service.CalcularHash(caminhoModificado));
            }
            finally
            {
                File.Delete(caminhoOriginal);
                File.Delete(caminhoModificado);
            }
        }

        [Fact]
        public void CalcularHash_ComCampoCujoNomeSoContemPrefixoVolatil_NaoIgnoraALinha()
        {
            // "HANDLEDESC" não é o campo "HANDLE" — não deve ser tratado como volátil.
            const string comCampoParecido = "@NEWFILE\n\t\t\tHANDLEDESC|abc\n";
            const string comCampoParecidoMudado = "@NEWFILE\n\t\t\tHANDLEDESC|xyz\n";

            var caminhoA = CriarArquivoTemporario(comCampoParecido);
            var caminhoB = CriarArquivoTemporario(comCampoParecidoMudado);
            try
            {
                Assert.NotEqual(_service.CalcularHash(caminhoA), _service.CalcularHash(caminhoB));
            }
            finally
            {
                File.Delete(caminhoA);
                File.Delete(caminhoB);
            }
        }

        private static string CriarArquivoTemporario(string conteudo)
        {
            var caminho = Path.Combine(Path.GetTempPath(), "JrToolsTests_Rpt_" + Guid.NewGuid().ToString("N") + ".rpt");
            File.WriteAllText(caminho, conteudo);
            return caminho;
        }
    }
}
