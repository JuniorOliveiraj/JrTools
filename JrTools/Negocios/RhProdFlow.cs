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
using Windows.System.Profile;

namespace JrTools.Negocios
{
    public class RhProdFlow
    {
        private readonly GitService _gitService;

        public RhProdFlow()
        {
            _gitService = new GitService();
        }

        public async Task<(bool Success, string Logs, string ErrorMessage)> ExecutarAsync(
            PageProdutoDataObject dto,
            IProgress<string>? progresso = null)
        {
            var logs = new StringBuilder();

            void Log(string mensagem)
            {
                logs.AppendLine(mensagem);
                progresso?.Report(mensagem);
            }

            try
            {

                var branch = string.IsNullOrWhiteSpace(dto.BreachEspecificaDeTrabalho)
                    ? dto.Breach
                    : dto.BreachEspecificaDeTrabalho;

                Log($"[INFO] Iniciando fluxo com branch: {branch}");

                var config = await ConfigHelper.LerConfiguracoesAsync();

                var cts = new CancellationTokenSource();
                Task? guardianTask = null;

                Log("[INFO] buscando tag");

                ValidarTagBranch comitTagdto = new ValidarTagBranch();

                if (!string.IsNullOrWhiteSpace(dto.TagEspecificaDeTrabalho))
                    comitTagdto = await VerificarSeBranchContemTagAsync(progresso, dto.TagEspecificaDeTrabalho, branch);

                if (!string.IsNullOrWhiteSpace(dto.TagEspecificaDeTrabalho) && !comitTagdto.status)
                    throw new FluxoException(comitTagdto.mensagem);


                var checkoutHandler = new GitRhProdHandler(_gitService, branch);
                if (!string.IsNullOrEmpty(dto.TagEspecificaDeTrabalho) && comitTagdto.status && !string.IsNullOrEmpty(comitTagdto.commit))
                {
                    progresso.Report($"[INFO] commit encontrado = {comitTagdto.commit}");
                    await checkoutHandler.ExecutarResetHardAsync(progresso, config.DiretorioProducao, comitTagdto.commit);
                }
                else
                {
                    progresso.Report($"[INFO] dando checkout em: {branch}");
                    await checkoutHandler.ExecutarCheckOutAsync(progresso, config.DiretorioProducao);
                }

                if (dto.AtualizarBreach)
                {
                    var pullHandler = new GitRhProdHandler(_gitService, branch);
                    await pullHandler.ExecutarPullAsync(progresso, config.DiretorioProducao);
                }

                if (dto.RunnerFechado)
                    await ProcessKiller.KillCs1Async(progresso);

                if (dto.BuilderFechado)
                    await ProcessKiller.KillBuilderAsync(progresso);

                if (dto.PrividerFechado)
                {
                    void LogGuardian(string msg) => progresso?.Report($"[GUARDIAN] {msg}");
                    guardianTask = IniciarGuardianBPrv230Async(cts.Token, new Progress<string>(LogGuardian));
                }

                if (dto.AtualizarBinarios)
                {
                    var binarioFlow = new BinariosProdutoSrv(dto);
                    await binarioFlow.ExecutarBuscarBinarios(progresso, config.DiretorioBinarios);
                }
                if (dto.BuildarProjeto)
                {
                    string caminhoSln = @"D:\Benner\fontes\rh\prod\dotnet\Solutions\Compilacao_Completa_SemWebApp.sln";
                    var buildHandler = new BinldarProjetoSrv();
                    await buildHandler.BuildarProjetoAsync(caminhoSln, progresso);
                }


                Log("[INFO] Fluxo concluído com sucesso!");
                return (true, logs.ToString(), string.Empty);
            }
            catch (Exception ex)
            {
                Log($"[ERRO] Exceção durante execução: {ex.Message}");
                return (false, logs.ToString(), $"Erro durante execução: {ex.Message}");
            }
        }



        private async Task IniciarGuardianBPrv230Async(CancellationToken cancellationToken, IProgress<string>? progresso = null)
        {
            progresso?.Report("[INFO] Iniciando guardião BPrv230...");
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessKiller.KillBPrv230Async(progresso);
                try
                {
                    progresso?.Report("[INFO] Tentando matar provider...");
                    await Task.Delay(30000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    progresso?.Report("[ERRO] Erro ao matar BPrv230.");
                    break;
                }
            }
            progresso?.Report("[INFO] Guardião BPrv230 finalizado.");
        }

        private async Task<ValidarTagBranch> VerificarSeBranchContemTagAsync(
            IProgress<string> progresso,
            string tag,
            string branchParaVerificar)
        {
            var config = await ConfigHelper.LerConfiguracoesAsync();
            progresso?.Report($"🔍 iniciando... {tag} {config.DiretorioProducao} ");

            var checkoutHandler = new GitRhProdHandler(_gitService, "");
            var branches = await checkoutHandler.ExecutarCheckOutPorTagAsync(progresso, config.DiretorioProducao, tag);
            ValidarTagBranch retorno = new ValidarTagBranch();

            bool contem = branches.Any(b =>
                b.Replace("*", "").Trim()
                 .Equals($"origin/{branchParaVerificar}", StringComparison.OrdinalIgnoreCase));

            if (!contem)
            {
                retorno.status = false;
                retorno.mensagem = $"⚠️  A tag {tag} NÃO está presente na branch origin/{branchParaVerificar}";
                progresso?.Report($"⚠️  A tag {tag} NÃO está presente na branch origin/{branchParaVerificar}");
            }

            // >>> NOVO: Obtém commits da tag e da branch para comparação
            var commitTag = await checkoutHandler.ObterCommitDeTagAsync(progresso, config.DiretorioProducao, tag);
            var commitBranch = await checkoutHandler.ObterCommitDeBranchAsync(progresso, config.DiretorioProducao, $"origin/{branchParaVerificar}");


            progresso?.Report($"🔍 Commit da TAG {tag}: {commitTag}");
            progresso?.Report($"🔍 Commit da BRANCH origin/{branchParaVerificar}: {commitBranch}");

            if (commitTag == commitBranch)
            {
                retorno.status = true;
                retorno.commit = commitBranch;
                progresso?.Report($"✅ Tag e branch apontam para o mesmo commit. {commitBranch}");
            }
            else
            {
                retorno.status = false;
                retorno.mensagem = $"⚠️ Tag e branch apontam para commits diferentes.";
                progresso?.Report($"⚠️ Tag e branch apontam para commits diferentes.");
            }

            return retorno;


        }

        private class ValidarTagBranch
        {
            public string? mensagem { get; set; }
            public bool status { get; set; }
            public string? commit { get; set; }
        }



    }
}
