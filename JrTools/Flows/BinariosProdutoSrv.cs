using JrTools.Dto;
using JrTools.Services;
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
            var branch = string.IsNullOrWhiteSpace(_param.BreachEspecificaDeTrabalho)
                ? _param.Breach
                : _param.BreachEspecificaDeTrabalho;

            var binarios = new BinarioService();
            var branchInfo = BuscarBranchDeTrabalho(branch);

            progresso.Report($"[INFO] Buscando binário para {branchInfo}...");
            progresso.Report($"[INFO] Buscando binário para {branchInfo}...");
            progresso.Report($"[INFO] Buscando binário para {branchInfo}...");

            BinarioInfoDataObject devBin = await binarios.ObterBinarioAsync(branchInfo);
            if (devBin == null)
            {
                progresso.Report($"[ERRO] Não foi possível localizar o binário para a branch '{branchInfo}'.");
                throw new FluxoException($"Binário não encontrado: {branchInfo}");
                 
            }

            devBin.destino = destino;

            progresso.Report($"[INFO] Extraindo binário para {devBin.destino}...");
            await binarios.ExtrairBinarioAsync(devBin, progresso);

            progresso.Report($"[INFO] Binário {devBin.NomeOriginal} processado com sucesso!");
        }

        private string BuscarBranchDeTrabalho(string branch)
        {
            var service = new branchName();
           return service.ObterBranchInfo(branch).Branch;
        }
    }
}
