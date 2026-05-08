using JrTools.Dto;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Abstração para leitura e persistência de configurações.
    /// Permite substituição por fake em testes (evita dependência de sistema de arquivos).
    /// </summary>
    public interface IConfigHelper
    {
        /// <summary>Lê as configurações do arquivo config.json.</summary>
        Task<ConfiguracoesdataObject> LerConfiguracoesAsync();

        /// <summary>Salva as configurações no arquivo config.json.</summary>
        Task SalvarConfiguracoesAsync(ConfiguracoesdataObject config);
    }
}
