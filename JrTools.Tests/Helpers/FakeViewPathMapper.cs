using System.Collections.Generic;
using JrTools.Services;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Implementação fake de <see cref="IViewPathMapper"/> para uso em testes.
    /// Permite configurar respostas e verificar chamadas sem acessar o sistema de arquivos.
    /// </summary>
    public class FakeViewPathMapper : IViewPathMapper
    {
        /// <summary>Número de vezes que <see cref="EnsureInitialized"/> foi chamado.</summary>
        public int EnsureInitializedCallCount { get; private set; }

        /// <summary>Último diretório passado para <see cref="EnsureInitialized"/>.</summary>
        public string? LastInitializedDirectory { get; private set; }

        /// <summary>
        /// Dicionário configurável de respostas para <see cref="MapearCaminhosEmLote"/>.
        /// Chave: nome da view; Valor: lista de caminhos de navegação.
        /// Views não presentes neste dicionário retornam lista vazia.
        /// </summary>
        public Dictionary<string, List<string>> Respostas { get; } = new();

        /// <inheritdoc />
        public void EnsureInitialized(string diretorio)
        {
            EnsureInitializedCallCount++;
            LastInitializedDirectory = diretorio;
        }

        /// <inheritdoc />
        public Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes, string diretorio)
        {
            EnsureInitialized(diretorio);

            var resultado = new Dictionary<string, List<string>>();
            foreach (var view in visoes)
            {
                resultado[view] = Respostas.TryGetValue(view, out var caminhos)
                    ? caminhos
                    : new List<string>();
            }
            return resultado;
        }
    }
}
