using System;
using System.IO;
using JrTools.Services;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes para <see cref="BServerConnectionService.ResolverCaminhoDll"/> — nem todo
    /// ambiente organiza os binários numa subpasta "delphi" (esse layout vem do fluxo de
    /// download/atualização de binários do próprio JrTools); em vários setups o diretório
    /// configurado já contém a DLL diretamente. O método precisa achar os dois casos.
    /// </summary>
    public class BServerConnectionServiceTests : IDisposable
    {
        private readonly string _testDirectory;

        public BServerConnectionServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "JrToolsTests_BServerDll_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, recursive: true);
            }
            catch { }
        }

        [Fact]
        public void ResolverCaminhoDll_ComDiretorioVazio_RetornaNull()
        {
            Assert.Null(BServerConnectionService.ResolverCaminhoDll(""));
            Assert.Null(BServerConnectionService.ResolverCaminhoDll("   "));
            Assert.Null(BServerConnectionService.ResolverCaminhoDll(null!));
        }

        [Fact]
        public void ResolverCaminhoDll_QuandoDllEstaNaSubpastaDelphi_RetornaEsseCaminho()
        {
            var pastaDelphi = Path.Combine(_testDirectory, "delphi");
            Directory.CreateDirectory(pastaDelphi);
            var caminhoEsperado = Path.Combine(pastaDelphi, "Benner.Tecnologia.BServer.Clients.dll");
            File.WriteAllText(caminhoEsperado, "");

            var resultado = BServerConnectionService.ResolverCaminhoDll(_testDirectory);

            Assert.Equal(caminhoEsperado, resultado);
        }

        [Fact]
        public void ResolverCaminhoDll_QuandoDllEstaDiretoNoDiretorio_RetornaEsseCaminho()
        {
            var caminhoEsperado = Path.Combine(_testDirectory, "Benner.Tecnologia.BServer.Clients.dll");
            File.WriteAllText(caminhoEsperado, "");

            var resultado = BServerConnectionService.ResolverCaminhoDll(_testDirectory);

            Assert.Equal(caminhoEsperado, resultado);
        }

        [Fact]
        public void ResolverCaminhoDll_QuandoDllExisteNosDoisLugares_PrefereASubpastaDelphi()
        {
            var pastaDelphi = Path.Combine(_testDirectory, "delphi");
            Directory.CreateDirectory(pastaDelphi);
            var caminhoComSubpasta = Path.Combine(pastaDelphi, "Benner.Tecnologia.BServer.Clients.dll");
            File.WriteAllText(caminhoComSubpasta, "");
            File.WriteAllText(Path.Combine(_testDirectory, "Benner.Tecnologia.BServer.Clients.dll"), "");

            var resultado = BServerConnectionService.ResolverCaminhoDll(_testDirectory);

            Assert.Equal(caminhoComSubpasta, resultado);
        }

        [Fact]
        public void ResolverCaminhoDll_QuandoDllNaoExisteEmLugarNenhum_RetornaCaminhoComSubpastaDelphi()
        {
            var esperado = Path.Combine(_testDirectory, "delphi", "Benner.Tecnologia.BServer.Clients.dll");

            var resultado = BServerConnectionService.ResolverCaminhoDll(_testDirectory);

            Assert.Equal(esperado, resultado);
        }
    }
}
