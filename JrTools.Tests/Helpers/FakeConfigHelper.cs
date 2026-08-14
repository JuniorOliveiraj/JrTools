using JrTools.Dto;
using JrTools.Services;
using System.Threading.Tasks;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Implementação fake de <see cref="IConfigHelper"/> para uso em testes.
    /// Permite configurar a resposta de leitura e verificar chamadas de salvamento.
    /// </summary>
    public class FakeConfigHelper : IConfigHelper
    {
        /// <summary>Configuração retornada por <see cref="LerConfiguracoesAsync"/>.</summary>
        public ConfiguracoesdataObject ConfiguracaoParaRetornar { get; set; } = new ConfiguracoesdataObject
        {
            DiretorioBinarios = string.Empty,
            DiretorioProducao = string.Empty,
            DiretorioEspecificos = string.Empty
        };

        /// <summary>Última configuração passada para <see cref="SalvarConfiguracoesAsync"/>.</summary>
        public ConfiguracoesdataObject? UltimaConfiguracaoSalva { get; private set; }

        /// <summary>Número de vezes que <see cref="SalvarConfiguracoesAsync"/> foi chamado.</summary>
        public int SalvarCallCount { get; private set; }

        /// <inheritdoc />
        public Task<ConfiguracoesdataObject> LerConfiguracoesAsync() =>
            Task.FromResult(ConfiguracaoParaRetornar);

        /// <inheritdoc />
        public Task SalvarConfiguracoesAsync(ConfiguracoesdataObject config)
        {
            UltimaConfiguracaoSalva = config;
            SalvarCallCount++;
            return Task.CompletedTask;
        }
    }
}
