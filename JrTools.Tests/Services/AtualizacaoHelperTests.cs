using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JrTools.Dto;
using JrTools.Services.Db;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes unitários para <see cref="AtualizacaoHelper"/> — persistência do estado de
    /// checagem de auto-update (último check e última tag avisada por modal).
    /// Isola a pasta base via <see cref="AtualizacaoHelper.PastaBaseParaTestes"/> — no
    /// Windows, Environment.GetFolderPath(SpecialFolder.LocalApplicationData) ignora
    /// SetEnvironmentVariable, então não dá pra redirecionar via variável de ambiente.
    /// </summary>
    public class AtualizacaoHelperTests : IDisposable
    {
        private readonly string _testDirectory;

        public AtualizacaoHelperTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "JrToolsTests_Atualizacao_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            AtualizacaoHelper.PastaBaseParaTestes = _testDirectory;
        }

        public void Dispose()
        {
            AtualizacaoHelper.PastaBaseParaTestes = null;
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, recursive: true);
            }
            catch { }
        }

        [Fact]
        public async Task LerAsync_WhenFileDoesNotExist_ReturnsDefaultConfig()
        {
            var result = await AtualizacaoHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Equal(DateTime.MinValue, result.UltimaVerificacaoUtc);
            Assert.Equal(string.Empty, result.UltimaTagModalExibida);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsCorrupted_ReturnsDefaultConfig()
        {
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            await File.WriteAllTextAsync(Path.Combine(jrToolsDir, "atualizacao.json"), "{ isso não é json }");

            var result = await AtualizacaoHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.UltimaTagModalExibida);
        }

        [Fact]
        public async Task SalvarAsync_ThenLerAsync_RoundTripsCorrectamente()
        {
            var esperado = new AtualizacaoConfigDto
            {
                UltimaVerificacaoUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                UltimaTagModalExibida = "build-42-abc1234"
            };

            await AtualizacaoHelper.SalvarAsync(esperado);
            var carregado = await AtualizacaoHelper.LerAsync();

            Assert.Equal(esperado.UltimaVerificacaoUtc, carregado.UltimaVerificacaoUtc);
            Assert.Equal(esperado.UltimaTagModalExibida, carregado.UltimaTagModalExibida);
        }

        [Fact]
        public async Task SalvarAsync_WhenDirectoryDoesNotExist_CreatesDirectoryStructure()
        {
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            if (Directory.Exists(jrToolsDir))
                Directory.Delete(jrToolsDir, recursive: true);

            await AtualizacaoHelper.SalvarAsync(new AtualizacaoConfigDto { UltimaTagModalExibida = "build-1-abc" });

            Assert.True(Directory.Exists(jrToolsDir));
            Assert.True(File.Exists(Path.Combine(jrToolsDir, "atualizacao.json")));
        }

        [Fact]
        public async Task SalvarAsync_WhenFileAlreadyExists_OverwritesExistingFile()
        {
            await AtualizacaoHelper.SalvarAsync(new AtualizacaoConfigDto { UltimaTagModalExibida = "build-1-abc" });
            await AtualizacaoHelper.SalvarAsync(new AtualizacaoConfigDto { UltimaTagModalExibida = "build-2-def" });

            var resultado = await AtualizacaoHelper.LerAsync();

            Assert.Equal("build-2-def", resultado.UltimaTagModalExibida);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsEmpty_ReturnsDefaultConfig()
        {
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            await File.WriteAllTextAsync(Path.Combine(jrToolsDir, "atualizacao.json"), "");

            var result = await AtualizacaoHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.UltimaTagModalExibida);
        }
    }
}
