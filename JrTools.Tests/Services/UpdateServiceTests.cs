using System;
using System.IO;
using JrTools.Services;
using JrTools.Tests.Helpers;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes unitários para <see cref="UpdateService"/> — a lógica de comparação de versão,
    /// leitura de version.txt e geração do script de atualização não dependem de rede nem do
    /// WinUI, então são testadas diretamente. O fluxo que dispara o processo real
    /// (<see cref="UpdateService.PrepararEReiniciar"/>) é testado com <see cref="FakeProcessLauncher"/>,
    /// igual ao padrão já usado em <c>BinarioDelphiExplorerViewModel</c>.
    /// </summary>
    public class UpdateServiceTests
    {
        // ── ExtrairRunNumber ────────────────────────────────────────────────────

        [Theory]
        [InlineData("build-42-abc1234", 42)]
        [InlineData("build-1-a", 1)]
        [InlineData("build-1000-deadbee", 1000)]
        public void ExtrairRunNumber_ComTagValida_RetornaONumeroDoRun(string tag, int esperado)
        {
            Assert.Equal(esperado, UpdateService.ExtrairRunNumber(tag));
        }

        [Theory]
        [InlineData("v1.0.0")]
        [InlineData("")]
        [InlineData("release-42")]
        [InlineData("build-abc-def")]
        public void ExtrairRunNumber_ComTagInvalida_RetornaMenosUm(string tag)
        {
            Assert.Equal(-1, UpdateService.ExtrairRunNumber(tag));
        }

        // ── VersaoAtual(baseDirectory) ───────────────────────────────────────────

        [Fact]
        public void VersaoAtual_QuandoArquivoNaoExiste_RetornaNull()
        {
            var dir = CriarDiretorioTemporario();
            try
            {
                Assert.Null(UpdateService.VersaoAtual(dir));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void VersaoAtual_ComConteudoValido_RetornaTagTrimada()
        {
            var dir = CriarDiretorioTemporario();
            try
            {
                File.WriteAllText(Path.Combine(dir, "version.txt"), "  build-7-abcdef1  \n");
                Assert.Equal("build-7-abcdef1", UpdateService.VersaoAtual(dir));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void VersaoAtual_ComArquivoVazio_RetornaNull()
        {
            var dir = CriarDiretorioTemporario();
            try
            {
                File.WriteAllText(Path.Combine(dir, "version.txt"), "   ");
                Assert.Null(UpdateService.VersaoAtual(dir));
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        private static string CriarDiretorioTemporario()
        {
            var dir = Path.Combine(Path.GetTempPath(), "JrToolsTests_Update_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ── AnalisarRelease ──────────────────────────────────────────────────────

        private const string ReleaseComZip = @"[
          {
            ""tag_name"": ""build-50-deadbee"",
            ""html_url"": ""https://github.com/JuniorOliveiraj/JrTools/releases/tag/build-50-deadbee"",
            ""assets"": [
              { ""name"": ""JrTools-win-x64-build-50-deadbee.zip"", ""browser_download_url"": ""https://github.com/download/JrTools.zip"" }
            ]
          }
        ]";

        [Fact]
        public void AnalisarRelease_QuandoHaVersaoMaisNovaComZip_RetornaAtualizacao()
        {
            var resultado = UpdateService.AnalisarRelease(ReleaseComZip, runAtual: 10);

            Assert.NotNull(resultado);
            Assert.Equal("build-50-deadbee", resultado!.Tag);
            Assert.Equal(50, resultado.RunNumber);
            Assert.Equal("https://github.com/download/JrTools.zip", resultado.DownloadUrl);
            Assert.Equal("https://github.com/JuniorOliveiraj/JrTools/releases/tag/build-50-deadbee", resultado.HtmlUrl);
        }

        [Fact]
        public void AnalisarRelease_QuandoRunNumberIgualAoAtual_RetornaNull()
        {
            Assert.Null(UpdateService.AnalisarRelease(ReleaseComZip, runAtual: 50));
        }

        [Fact]
        public void AnalisarRelease_QuandoRunNumberMenorQueOAtual_RetornaNull()
        {
            Assert.Null(UpdateService.AnalisarRelease(ReleaseComZip, runAtual: 100));
        }

        [Fact]
        public void AnalisarRelease_SemAssetZip_RetornaNull()
        {
            const string json = @"[
              { ""tag_name"": ""build-50-deadbee"", ""assets"": [
                  { ""name"": ""readme.txt"", ""browser_download_url"": ""https://x/readme.txt"" }
              ] }
            ]";

            Assert.Null(UpdateService.AnalisarRelease(json, runAtual: 10));
        }

        [Fact]
        public void AnalisarRelease_SemAssets_RetornaNull()
        {
            const string json = @"[ { ""tag_name"": ""build-50-deadbee"", ""assets"": [] } ]";
            Assert.Null(UpdateService.AnalisarRelease(json, runAtual: 10));
        }

        [Fact]
        public void AnalisarRelease_ListaVazia_RetornaNull()
        {
            Assert.Null(UpdateService.AnalisarRelease("[]", runAtual: 10));
        }

        [Fact]
        public void AnalisarRelease_TagForaDoPadrao_RetornaNull()
        {
            const string json = @"[ { ""tag_name"": ""v2.0.0"", ""assets"": [
                { ""name"": ""app.zip"", ""browser_download_url"": ""https://x/app.zip"" }
            ] } ]";

            Assert.Null(UpdateService.AnalisarRelease(json, runAtual: 10));
        }

        [Fact]
        public void AnalisarRelease_QuandoMaisRecenteTemTagForaDoPadrao_PulaEUsaAProximaValida()
        {
            // Release manual/hotfix no topo (tag fora do padrão build-<N>-<sha>) não deve
            // esconder a build automática mais recente logo depois dela na lista.
            const string json = @"[
              { ""tag_name"": ""v2.0.0-hotfix"", ""assets"": [
                  { ""name"": ""app.zip"", ""browser_download_url"": ""https://x/hotfix.zip"" }
              ] },
              { ""tag_name"": ""build-33-abc1234"", ""html_url"": ""https://x/build-33"", ""assets"": [
                  { ""name"": ""JrTools-win-x64-build-33-abc1234.zip"", ""browser_download_url"": ""https://x/build33.zip"" }
              ] }
            ]";

            var resultado = UpdateService.AnalisarRelease(json, runAtual: 10);

            Assert.NotNull(resultado);
            Assert.Equal("build-33-abc1234", resultado!.Tag);
            Assert.Equal(33, resultado.RunNumber);
            Assert.Equal("https://x/build33.zip", resultado.DownloadUrl);
        }

        [Fact]
        public void AnalisarRelease_QuandoReleaseMaisAntigaApareceAntesDaMaisNovaNaLista_NaoDesisteEAchaAMaisNova()
        {
            // A API não garante ordem estritamente decrescente por run number dentro do
            // per_page=5 (ex.: uma release republicada/editada muda seu created_at sem mudar
            // a tag) — uma entrada <= runAtual aparecendo antes de uma mais nova não pode fazer
            // a busca desistir cedo demais e ignorar a mais nova que vem logo depois.
            const string json = @"[
              { ""tag_name"": ""build-9-1e581b8"", ""html_url"": ""https://x/build-9"", ""assets"": [
                  { ""name"": ""JrTools-win-x64-build-9-1e581b8.zip"", ""browser_download_url"": ""https://x/build9.zip"" }
              ] },
              { ""tag_name"": ""build-11-6e3ef5d"", ""html_url"": ""https://x/build-11"", ""assets"": [
                  { ""name"": ""JrTools-win-x64-build-11-6e3ef5d.zip"", ""browser_download_url"": ""https://x/build11.zip"" }
              ] }
            ]";

            var resultado = UpdateService.AnalisarRelease(json, runAtual: 9);

            Assert.NotNull(resultado);
            Assert.Equal("build-11-6e3ef5d", resultado!.Tag);
            Assert.Equal(11, resultado.RunNumber);
            Assert.Equal("https://x/build11.zip", resultado.DownloadUrl);
        }

        [Fact]
        public void AnalisarRelease_JsonInvalido_RetornaNullSemLancarExcecao()
        {
            Assert.Null(UpdateService.AnalisarRelease("isso não é json", runAtual: 10));
        }

        [Fact]
        public void AnalisarRelease_SemPropriedadeTagName_RetornaNullSemLancarExcecao()
        {
            const string json = @"[ { ""assets"": [] } ]";
            Assert.Null(UpdateService.AnalisarRelease(json, runAtual: 10));
        }

        // ── GerarScript ──────────────────────────────────────────────────────────

        [Fact]
        public void GerarScript_ContemComandosEsperadosComOsCaminhosCorretos()
        {
            var script = UpdateService.GerarScript(
                pid: 4242,
                stagingDir: @"C:\Temp\JrTools_Update\build-5-abc",
                installDir: @"C:\Program Files\JrTools",
                exePath: @"C:\Program Files\JrTools\JrTools.exe",
                zipPath: @"C:\Temp\JrTools_Update\build-5-abc.zip");

            Assert.Contains("Wait-Process -Id 4242", script);
            Assert.Contains(@"robocopy ""C:\Temp\JrTools_Update\build-5-abc"" ""C:\Program Files\JrTools"" /MIR", script);
            Assert.Contains(@"Start-Process -FilePath ""C:\Program Files\JrTools\JrTools.exe""", script);
            Assert.Contains(@"Remove-Item -LiteralPath ""C:\Temp\JrTools_Update\build-5-abc"" -Recurse -Force", script);
            Assert.Contains(@"Remove-Item -LiteralPath ""C:\Temp\JrTools_Update\build-5-abc.zip"" -Force", script);
        }

        // ── PrepararEReiniciar ───────────────────────────────────────────────────

        [Fact]
        public void PrepararEReiniciar_DisparaPowershellComOScriptEFechaOApp()
        {
            var launcher = new FakeProcessLauncher();
            var fechou = false;
            var service = new UpdateService(launcher, () => fechou = true);

            var stagingDir = Path.Combine(Path.GetTempPath(), "JrTools_Update", "build-99-teste");

            try
            {
                service.PrepararEReiniciar(stagingDir);

                Assert.Equal(1, launcher.CallCount);
                Assert.Equal("powershell.exe", launcher.LastFileName);
                Assert.Contains("-ExecutionPolicy Bypass", launcher.LastArguments);
                Assert.Contains("updater.ps1", launcher.LastArguments);
                Assert.True(fechou);

                var scriptPath = Path.Combine(Path.GetTempPath(), "JrTools_Update", "updater.ps1");
                Assert.True(File.Exists(scriptPath));
                var conteudo = File.ReadAllText(scriptPath);
                Assert.Contains($"Wait-Process -Id {Environment.ProcessId}", conteudo);
                Assert.Contains(stagingDir, conteudo);
            }
            finally
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), "JrTools_Update", "updater.ps1");
                if (File.Exists(scriptPath)) File.Delete(scriptPath);
            }
        }
    }
}
