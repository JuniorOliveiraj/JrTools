using JrTools.Dto;
using JrTools.Flows;
using JrTools.Flows.Build;
using JrTools.Services;
using JrTools.Utils;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Negocios
{
    public class RhProdFlow
    {
        private readonly GitService _gitService;
        private IProgress<string>? _progresso;
        private StringBuilder _logs;

        public RhProdFlow()
        {
            _gitService = new GitService();
            _logs = new StringBuilder();
        }

        public async Task<(bool Success, string Logs, string ErrorMessage)> ExecutarAsync(
            PageProdutoDataObject dto,
            IProgress<string>? progresso = null)
        {
            _progresso = progresso;
            _logs.Clear();

            try
            {
                var branch = ObterBranchAlvo(dto);
                Log($"[INFO] Iniciando fluxo com branch: {branch}");

                var config = await ConfigHelper.LerConfiguracoesAsync();

                // 1. Validação e Checkout Git
                await GerenciarGitAsync(dto, branch, config.DiretorioProducao);

                // 2. Gerenciamento de Processos (Kill)
                await GerenciarProcessosAnterioresAsync(dto);

                // 3. Guardião (Provider)
                CancellationTokenSource? ctsGuardian = null;
                if (dto.PrividerFechado)
                {
                    ctsGuardian = new CancellationTokenSource();
                    _ = IniciarGuardianBPrv230Async(ctsGuardian.Token, CreateProgressForGuardian());
                }

                // 4. Atualização de Binários
                if (dto.AtualizarBinarios)
                {
                    await AtualizarBinariosAsync(dto, config.DiretorioBinarios);
                }

                // 5. Compilação
                if (dto.BuildarProjeto)
                {
                    await BuildarProjetoAsync(@"D:\Benner\fontes\rh\prod\dotnet\Solutions\Compilacao_Completa_SemWebApp.sln");
                }

                // Cleanup do Guardião se foi iniciado
                ctsGuardian?.Cancel();

                Log("[INFO] Fluxo concluído com sucesso!");
                return (true, _logs.ToString(), string.Empty);
            }
            catch (Exception ex)
            {
                Log($"[ERRO] Exceção durante execução: {ex.Message}");
                return (false, _logs.ToString(), $"Erro durante execução: {ex.Message}");
            }
        }

        #region Private Methods - Logic Sections

        private string ObterBranchAlvo(PageProdutoDataObject dto)
        {
            return string.IsNullOrWhiteSpace(dto.BreachEspecificaDeTrabalho)
                ? dto.Breach
                : dto.BreachEspecificaDeTrabalho;
        }

        private async Task GerenciarGitAsync(PageProdutoDataObject dto, string branch, string diretorioProducao)
        {
            Log("[INFO] Verificando Tag e Branch...");
            
            ValidarTagBranch validacaoTag = new ValidarTagBranch { Status = false };

            if (!string.IsNullOrWhiteSpace(dto.TagEspecificaDeTrabalho))
            {
                validacaoTag = await VerificarSeBranchContemTagAsync(dto.TagEspecificaDeTrabalho, branch, diretorioProducao);
                
                if (!validacaoTag.Status)
                    throw new FluxoException(validacaoTag.Mensagem ?? "Erro na validação da tag.");
            }

            var gitHandler = new GitRhProdHandler(_gitService, branch);

            if (!string.IsNullOrEmpty(dto.TagEspecificaDeTrabalho) && validacaoTag.Status && !string.IsNullOrEmpty(validacaoTag.Commit))
            {
                Log($"[INFO] Commit encontrado = {validacaoTag.Commit}");
                await gitHandler.ExecutarResetHardAsync(_progresso, diretorioProducao, validacaoTag.Commit);
            }
            else
            {
                Log($"[INFO] Dando checkout em: {branch}");
                await gitHandler.ExecutarCheckOutAsync(_progresso, diretorioProducao);
            }

            if (dto.AtualizarBreach)
            {
                Log($"[INFO] Atualizando branch {branch}...");
                await gitHandler.ExecutarPullAsync(_progresso, diretorioProducao);
            }
        }

        private async Task GerenciarProcessosAnterioresAsync(PageProdutoDataObject dto)
        {
            if (dto.RunnerFechado)
                await ProcessKiller.KillCs1Async(_progresso);

            if (dto.BuilderFechado)
                await ProcessKiller.KillBuilderAsync(_progresso);
        }

        private async Task AtualizarBinariosAsync(PageProdutoDataObject dto, string diretorioBinarios)
        {
            var binarioFlow = new BinariosProdutoSrv(dto);
            await binarioFlow.ExecutarBuscarBinarios(_progresso, diretorioBinarios);
        }

        private async Task BuildarProjetoAsync(string caminhoSln)
        {
            var buildHandler = new BinldarProjetoSrv();
            await buildHandler.BuildarProjetoAsync(caminhoSln, _progresso);
        }

        private async Task<ValidarTagBranch> VerificarSeBranchContemTagAsync(string tag, string branchParaVerificar, string diretorioProducao)
        {
            Log($"🔍 Iniciando verificação... Tag: {tag} | Dir: {diretorioProducao} ");

            var gitHandler = new GitRhProdHandler(_gitService, "");
            var branches = await gitHandler.ExecutarCheckOutPorTagAsync(_progresso, diretorioProducao, tag);
            
            var retorno = new ValidarTagBranch();

            string branchTarget = $"origin/{branchParaVerificar}";

            bool contem = branches.Any(b =>
                b.Replace("*", "").Trim()
                 .Equals(branchTarget, StringComparison.OrdinalIgnoreCase));

            if (!contem)
            {
                retorno.Status = false;
                retorno.Mensagem = $"⚠️ A tag {tag} NÃO está presente na branch {branchTarget}";
                Log(retorno.Mensagem);
            }

            // Comparar Commits
            var commitTag = await gitHandler.ObterCommitDeTagAsync(_progresso, diretorioProducao, tag);
            var commitBranch = await gitHandler.ObterCommitDeBranchAsync(_progresso, diretorioProducao, branchTarget);

            Log($"🔍 Commit da TAG {tag}: {commitTag}");
            Log($"🔍 Commit da BRANCH {branchTarget}: {commitBranch}");

            if (commitTag == commitBranch)
            {
                retorno.Status = true;
                retorno.Commit = commitBranch;
                Log($"✅ Tag e branch apontam para o mesmo commit. {commitBranch}");
            }
            else
            {
                retorno.Status = false;
                retorno.Mensagem = $"⚠️ Tag e branch apontam para commits diferentes.";
                Log(retorno.Mensagem);
            }

            return retorno;
        }

        #endregion

        #region Helpers

        private void Log(string mensagem)
        {
            _logs.AppendLine(mensagem);
            _progresso?.Report(mensagem);
        }

        private IProgress<string> CreateProgressForGuardian()
        {
            return new Progress<string>(msg => _progresso?.Report($"[GUARDIAN] {msg}"));
        }

        private async Task IniciarGuardianBPrv230Async(CancellationToken cancellationToken, IProgress<string>? progresso = null)
        {
            progresso?.Report("Iniciando guardião BPrv230...");
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessKiller.KillBPrv230Async(progresso);
                try
                {
                    progresso?.Report("Tentando matar provider...");
                    await Task.Delay(30000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            progresso?.Report("Guardião BPrv230 finalizado.");
        }

        #endregion
    }
}
