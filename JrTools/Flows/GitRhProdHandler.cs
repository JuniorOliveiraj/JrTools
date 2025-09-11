using JrTools.Dto;
using JrTools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public async Task ExecutarResetHardAsync(IProgress<string> progresso, string diretorio, string commit)
        {
            if (string.IsNullOrWhiteSpace(commit))
                throw new ArgumentException("Commit não pode ser vazio para reset.", nameof(commit));

            await ExecutarGitCommandAsync(
                $"reset --hard {commit}",
                progresso,
                $"[RESET HARD {commit}]",
                diretorio);
        }

        public async Task<string> ObterCommitDeTagAsync(IProgress<string> progresso, string diretorio, string tag)
        {
            return (await ExecutarGitCommandComRetornoAsync(
                $"rev-list -n 1 {tag}",
                progresso,
                $"[OBTER COMMIT DA TAG {tag}]",
                diretorio)).Trim();
        }

        public async Task<string> ObterCommitDeBranchAsync(IProgress<string> progresso, string diretorio, string branch)
        {
            return (await ExecutarGitCommandComRetornoAsync(
                $"rev-parse {branch}",
                progresso,
                $"[OBTER COMMIT DA BRANCH {branch}]",
                diretorio)).Trim();
        }

        public async Task<List<string>> ExecutarCheckOutPorTagAsync(IProgress<string> progresso, string diretorio, string tag)
        {
            // 1. Atualiza as referências e tags
            await ExecutarGitCommandAsync("fetch --all --tags", progresso, "[GIT FETCH]", diretorio);

            // 2. Faz checkout na tag especificada
            await ExecutarGitCommandAsync($"checkout tags/{tag}", progresso, $"[CHECKOUT TAG {tag}]", diretorio);

            // 3. Lista branches que contêm a tag
            var resultado = await ExecutarGitCommandComRetornoAsync(
                $"branch -r --contains {tag}",
                progresso,
                "[VERIFICANDO TAG NAS BRANCHES]",
                diretorio
            );

            // Quebra a saída em linhas, remove vazios e normaliza espaços
            var branches = resultado
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToList();

            if (branches.Any(b => b.Contains("origin/producao_09.00", StringComparison.OrdinalIgnoreCase)))
                progresso?.Report($"✅ A tag {tag} está contida na branch origin/producao_09.00");
            else
                progresso?.Report($"⚠️  A tag {tag} NÃO está na branch origin/producao_09.00");

            return branches;
        }




        private async Task<string> ExecutarGitCommandComRetornoAsync(string arguments, IProgress<string> progresso, string titulo, string diretorio)
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

            var saida = new StringBuilder();

            using var processo = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs = new TaskCompletionSource<bool>();

            processo.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    saida.AppendLine(e.Data);
                    progresso?.Report($"{titulo}: {e.Data}");
                }
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

            await tcs.Task;

            if (processo.ExitCode != 0)
                throw new Exception($"{titulo} terminou com erro. Código de saída: {processo.ExitCode}");

            progresso?.Report($"{titulo} concluído.");

            return saida.ToString();
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
