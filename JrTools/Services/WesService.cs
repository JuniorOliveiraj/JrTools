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

        public Task ConfigSetAsync(string servidor, string nomeSistema, string usuario, string senha, IProgress<string> progresso)
            => RunAsync($"config set -h {servidor} -s {nomeSistema} -u {usuario} -p {senha}", progresso, "[WES CONFIG SET]");

        public Task CacheClearAsync(IProgress<string> progresso)
            => RunAsync("cache clear", progresso, "[WES CACHE CLEAR]");

        public Task ArtifactsInstallAsync(IProgress<string> progresso)
            => RunAsync("artifacts install -s", progresso, "[WES ARTIFACTS INSTALL]");

        public Task PagesGenerateAsync(IProgress<string> progresso)
            => RunAsync("pages generate", progresso, "[WES PAGES GENERATE]");

        private async Task RunAsync(string arguments, IProgress<string> progresso, string titulo)
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
        }
    }
}
