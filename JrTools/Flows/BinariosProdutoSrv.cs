using JrTools.Dto;
using JrTools.Enums;
using JrTools.Services;
using JrTools.Services.Db;
using JrTools.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    public class BinariosProdutoSrv
    {
        private readonly PageProdutoDataObject _param;
        public BinariosProdutoSrv(PageProdutoDataObject dto)
        {
            _param = dto;
        }

        public async Task ExecutarBuscarBinarios(IProgress<string> progresso, string destino )
        {
            var branch = string.IsNullOrWhiteSpace(_param.BranchEspecificaDeTrabalho)
                ? _param.Branch
                : _param.BranchEspecificaDeTrabalho;

            var cfg = await ConfigHelper.LerConfiguracoesAsync();
            var branchLimpo = BuscarBranchDeTrabalho(branch);

            IBinarioSourceProvider provider;
            if (cfg.FonteBinarios == FonteBinarios.Jenkins)
            {
                var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();
                provider = new JenkinsBinarioProvider(cfg.JenkinsBaseUrl, cfg.JenkinsJobPath, dados.JenkinsUsuario, dados.JenkinsApiToken);
            }
            else
            {
                provider = new ServidorBinarioProvider(cfg.CaminhoServidorBinarios);
            }

            progresso.Report($"[INFO] Buscando binário para {branchLimpo}...");

            BinarioInfoDataObject devBin = await provider.ObterBinarioAsync(branchLimpo, progresso);
            if (devBin == null)
            {
                progresso.Report($"[ERRO] Não foi possível localizar o binário para a branch '{branchLimpo}'.");
                throw new FluxoException($"Binário não encontrado: {branchLimpo}");

            }

            devBin.destino = destino;

            progresso.Report($"[INFO] Extraindo binário para {devBin.destino}...");
            var extrator = new BinarioService();
            await extrator.ExtrairBinarioAsync(devBin, progresso);

            progresso.Report($"[INFO] Binário {devBin.NomeOriginal} processado com sucesso!");
        }

        private string BuscarBranchDeTrabalho(string branch)
        {
            var service = new BranchNameHelper();
            // Ex: sms/dev/09.00.00 → dev/09.00.00
            // Ex: sms/prd/09.00    → prd/09.00
            return service.ObterBranchInfo(branch).Branch;
        }
    }
}
