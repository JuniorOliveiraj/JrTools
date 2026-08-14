using JrTools.Dto;
using System;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Localiza (e, quando necessário, baixa) o zip de binários de uma branch para a pasta
    /// temporária (<see cref="BinarioService.PastaTemporaria"/>), pronto para extração.
    /// </summary>
    public interface IBinarioSourceProvider
    {
        /// <param name="branchLimpo">Branch com barra, já sem prefixo (ex.: "dev/09.00.00").</param>
        Task<BinarioInfoDataObject?> ObterBinarioAsync(string branchLimpo, IProgress<string>? progresso = null);
    }
}
