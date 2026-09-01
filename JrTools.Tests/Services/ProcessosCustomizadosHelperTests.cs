using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JrTools.Dto;
using JrTools.Services.Db;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes unitários para <see cref="ProcessosCustomizadosHelper"/> — persistência dos
    /// processos adicionados manualmente em "Fechar Processos" (nome + estado ligado/desligado).
    /// Isola a pasta base via <see cref="ProcessosCustomizadosHelper.PastaBaseParaTestes"/> —
    /// no Windows, Environment.GetFolderPath(SpecialFolder.LocalApplicationData) ignora
    /// SetEnvironmentVariable, então não dá pra redirecionar via variável de ambiente.
    /// </summary>
    public class ProcessosCustomizadosHelperTests : IDisposable
    {
        private readonly string _testDirectory;

        public ProcessosCustomizadosHelperTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "JrToolsTests_ProcessosCustomizados_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            ProcessosCustomizadosHelper.PastaBaseParaTestes = _testDirectory;
        }

        public void Dispose()
        {
            ProcessosCustomizadosHelper.PastaBaseParaTestes = null;
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, recursive: true);
            }
            catch { }
        }

        [Fact]
        public async Task LerAsync_WhenFileDoesNotExist_ReturnsEmptyConfig()
        {
            var result = await ProcessosCustomizadosHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Empty(result.Processos);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsCorrupted_ReturnsEmptyConfig()
        {
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            await File.WriteAllTextAsync(Path.Combine(jrToolsDir, "processos-customizados.json"), "{ isso não é json }");

            var result = await ProcessosCustomizadosHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Empty(result.Processos);
        }

        [Fact]
        public async Task SalvarAsync_ThenLerAsync_RoundTripsCorrectamente()
        {
            var esperado = new ProcessoCustomizadoConfig
            {
                Processos = new List<ProcessoCustomizadoItem>
                {
                    new() { Nome = "notepad", Habilitado = true },
                    new() { Nome = "chrome", Habilitado = false }
                }
            };

            await ProcessosCustomizadosHelper.SalvarAsync(esperado);
            var carregado = await ProcessosCustomizadosHelper.LerAsync();

            Assert.Equal(2, carregado.Processos.Count);
            Assert.Equal("notepad", carregado.Processos[0].Nome);
            Assert.True(carregado.Processos[0].Habilitado);
            Assert.Equal("chrome", carregado.Processos[1].Nome);
            Assert.False(carregado.Processos[1].Habilitado);
        }

        [Fact]
        public async Task SalvarAsync_WhenFileAlreadyExists_OverwritesExistingFile()
        {
            await ProcessosCustomizadosHelper.SalvarAsync(new ProcessoCustomizadoConfig
            {
                Processos = new List<ProcessoCustomizadoItem> { new() { Nome = "a", Habilitado = true } }
            });
            await ProcessosCustomizadosHelper.SalvarAsync(new ProcessoCustomizadoConfig
            {
                Processos = new List<ProcessoCustomizadoItem> { new() { Nome = "b", Habilitado = false } }
            });

            var resultado = await ProcessosCustomizadosHelper.LerAsync();

            Assert.Single(resultado.Processos);
            Assert.Equal("b", resultado.Processos[0].Nome);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsEmpty_ReturnsEmptyConfig()
        {
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            await File.WriteAllTextAsync(Path.Combine(jrToolsDir, "processos-customizados.json"), "");

            var result = await ProcessosCustomizadosHelper.LerAsync();

            Assert.NotNull(result);
            Assert.Empty(result.Processos);
        }
    }
}
