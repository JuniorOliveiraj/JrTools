using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class WesService
    {
        private readonly string _wesExePath;

        public WesService(string wesExePath)
        {
            _wesExePath = wesExePath;
        }

        public Task<int> ConfigSetAsync(string servidor, string nomeSistema, string usuario, string senha, IProgress<string> progresso)
            => RunAsync($"config set -h {servidor} -s {nomeSistema} -u {usuario} -p {senha}", progresso, "[WES CONFIG SET]");

        public Task<int> CacheClearAsync(IProgress<string> progresso)
            => RunAsync("cache clear", progresso, "[WES CACHE CLEAR]");

        public Task<int> ArtifactsInstallAsync(IProgress<string> progresso)
            => RunAsync("artifacts install -s", progresso, "[WES ARTIFACTS INSTALL]");

        public Task<int> ArtifactsInstallLayerAsync(string layer, IProgress<string> progresso)
            => RunAsync($"artifacts install -l {layer} -s -v", progresso, $"[WES ARTIFACTS INSTALL: {layer.ToUpper()}]");

        public Task<int> PagesGenerateAsync(IProgress<string> progresso)
            => RunAsync("pages generate", progresso, "[WES PAGES GENERATE]");

        public Task<int> ArtifactsInstallSelectiveAsync(string webAppPath, System.Collections.Generic.List<string> relativeFiles, IProgress<string> progresso)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string installerExe = System.IO.Path.Combine(webAppPath, "Bin", "BennerSmartInstaller.exe");

            if (!System.IO.File.Exists(installerExe))
            {
                installerExe = System.IO.Path.Combine(baseDir, "BennerSmartInstaller.exe");
            }

            if (!System.IO.File.Exists(installerExe))
            {
                installerExe = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, @"..\..\..\..\BennerSmartInstaller\bin\Debug\BennerSmartInstaller.exe"));
            }

            if (!System.IO.File.Exists(installerExe))
            {
                throw new System.IO.FileNotFoundException($"Utilitário BennerSmartInstaller.exe não encontrado em: {installerExe}");
            }

            string artifactsArg = string.Join(";", relativeFiles);
            string args = $"install -a \"{webAppPath}\" -f \"{artifactsArg}\"";

            return RunProcessAsync(installerExe, args, progresso, "[SMART INSTALL SELETIVO]");
        }

        private Task<int> RunProcessAsync(string exePath, string arguments, IProgress<string> progresso, string titulo)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var tcs = new TaskCompletionSource<bool>();
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"[ERRO]: {e.Data}"); };
            process.Exited += (s, e) => tcs.TrySetResult(true);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return Task.Run(async () =>
            {
                await tcs.Task;
                await process.WaitForExitAsync();
                return process.ExitCode;
            });
        }

        private async Task<int> RunAsync(string arguments, IProgress<string> progresso, string titulo)
        {
            progresso?.Report($"{titulo} iniciando...");

            var psi = new ProcessStartInfo
            {
                FileName = _wesExePath,
                Arguments = arguments,
                WorkingDirectory = System.IO.Path.GetDirectoryName(_wesExePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var tcs = new TaskCompletionSource<bool>();
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"{titulo}: {e.Data}"); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"{titulo} [ERRO]: {e.Data}"); };
            process.Exited += (s, e) => tcs.TrySetResult(true);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await tcs.Task;
            await process.WaitForExitAsync();

            progresso?.Report(process.ExitCode == 0
                ? $"{titulo} concluído com sucesso."
                : $"{titulo} finalizou com código {process.ExitCode}.");

            return process.ExitCode;
        }
    }
}
