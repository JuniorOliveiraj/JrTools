using JrTools.Dto;
using JrTools.Services;
using System;
using System.Collections.Generic;
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
            await _gitService.RunCommandWithProgressAsync("fetch", diretorio, progresso, "[GIT FETCH]");
            await _gitService.RunCommandWithProgressAsync("pull", diretorio, progresso, "[GIT PULL]");
        }

        public async Task ExecutarCheckOutAsync(IProgress<string> progresso, string diretorio)
        {
            await _gitService.RunCommandWithProgressAsync($"checkout {_branch}", diretorio, progresso, $"[CHECKOUT {_branch}]");
        }

        public async Task<string> ObterAlteracoesDeCommitAsync(
            IProgress<string> progresso,
            string diretorio,
            string commitId)
        {
            if (string.IsNullOrWhiteSpace(commitId))
                throw new ArgumentException("Commit não pode ser vazio.", nameof(commitId));

            return await _gitService.RunCommandWithProgressAsync(
                $"show {commitId}",
                diretorio,
                progresso,
                $"[GIT SHOW {commitId}]"
            );
        }


        public async Task ExecutarResetHardAsync(IProgress<string> progresso, string diretorio, string commit)
        {
            if (string.IsNullOrWhiteSpace(commit))
                throw new ArgumentException("Commit não pode ser vazio para reset.", nameof(commit));

            await _gitService.RunCommandWithProgressAsync(
                $"reset --hard {commit}",
                diretorio,
                progresso,
                $"[RESET HARD {commit}]");
        }

        public async Task<string> ObterCommitDeTagAsync(IProgress<string> progresso, string diretorio, string tag)
        {
            var result = await _gitService.RunCommandWithProgressAsync(
                $"rev-list -n 1 {tag}",
                diretorio,
                progresso,
                $"[OBTER COMMIT DA TAG {tag}]");
            return result.Trim();
        }

        public async Task<string> ObterCommitDeBranchAsync(IProgress<string> progresso, string diretorio, string branch)
        {
            var result = await _gitService.RunCommandWithProgressAsync(
                $"rev-parse {branch}",
                diretorio,
                progresso,
                $"[OBTER COMMIT DA BRANCH {branch}]");
            return result.Trim();
        }

        public async Task<List<string>> ExecutarCheckOutPorTagAsync(IProgress<string> progresso, string diretorio, string tag)
        {
            // 1. Atualiza as referências e tags
            await _gitService.RunCommandWithProgressAsync("fetch --all --tags", diretorio, progresso, "[GIT FETCH]");

            // 2. Faz checkout na tag especificada
            await _gitService.RunCommandWithProgressAsync($"checkout tags/{tag}", diretorio, progresso, $"[CHECKOUT TAG {tag}]");

            // 3. Lista branches que contêm a tag
            var resultado = await _gitService.RunCommandWithProgressAsync(
                $"branch -r --contains {tag}",
                diretorio,
                progresso,
                "[VERIFICANDO TAG NAS BRANCHES]"
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
    }

}
