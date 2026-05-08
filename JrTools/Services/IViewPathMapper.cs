using System.Collections.Generic;

namespace JrTools.Services
{
    /// <summary>
    /// Abstração para mapeamento de caminhos de navegação de views.
    /// Permite substituição por fake em testes (evita dependência de sistema de arquivos).
    /// </summary>
    public interface IViewPathMapper
    {
        /// <summary>
        /// Garante que o serviço está inicializado para o diretório informado.
        /// Se já foi inicializado para o mesmo diretório, não faz nada.
        /// </summary>
        /// <param name="diretorio">Diretório raiz contendo a pasta Pages.</param>
        void EnsureInitialized(string diretorio);

        /// <summary>
        /// Mapeia os caminhos de navegação para uma coleção de views em lote.
        /// Chama EnsureInitialized uma única vez antes de processar todas as views.
        /// </summary>
        /// <param name="visoes">Nomes das views a mapear.</param>
        /// <param name="diretorio">Diretório raiz contendo a pasta Pages.</param>
        /// <returns>Dicionário: nome da view → lista de caminhos de navegação.</returns>
        Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes, string diretorio);
    }
}
