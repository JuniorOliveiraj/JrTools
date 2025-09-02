using JrTools.Dto;
using JrTools.Services;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    public class GitRhProdHandler
    {
        private readonly GitService _gitService;
        private readonly string _branch;

        public GitRhProdHandler(GitService gitService, string branch)
        {
            _gitService = gitService;
            _branch = branch;
        }

        public async Task ExecutarPullAsync(IProgress<string> progresso, string diretorio)
        {
            await ExecutarGitCommandAsync("fetch", progresso, "[GIT PULL]", diretorio);
            await ExecutarGitCommandAsync("pull", progresso, "[GIT PULL]", diretorio);
        }

        public async Task ExecutarCheckOutAsync(IProgress<string> progresso, string diretorio)
        {
            await ExecutarGitCommandAsync($"checkout {_branch}", progresso, $"[CHECKOUT {_branch}]", diretorio);
        }

        private async Task ExecutarGitCommandAsync(string arguments, IProgress<string> progresso, string titulo, string diretorio)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = diretorio,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var processo = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<bool>();

            processo.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    progresso?.Report($"{titulo}: {e.Data}");
            };

            processo.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    progresso?.Report($"{titulo} [ERRO]: {e.Data}");
            };

            processo.Exited += (s, e) => tcs.SetResult(true);

            progresso?.Report($"{titulo} iniciando...");
            processo.Start();
            processo.BeginOutputReadLine();
            processo.BeginErrorReadLine();

            await tcs.Task; // espera terminar

            if (processo.ExitCode != 0)
                throw new Exception($"{titulo} terminou com erro. Código de saída: {processo.ExitCode}");

            progresso?.Report($"{titulo} concluído.");
        }
    }
       
}
