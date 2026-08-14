using System.Collections.Generic;

namespace JrTools.Dto
{
    /// <summary>
    /// Representa o resultado agrupado de busca para uma view específica.
    /// </summary>
    public class ViewResultadoItem
    {
        /// <summary>Nome da view buscada (ex.: "IWFuncionarios").</summary>
        public string NomeView { get; set; } = string.Empty;

        /// <summary>
        /// Lista de caminhos de navegação encontrados no formato
        /// "Título da Página > Widget [NomeDaView]".
        /// Lista vazia indica que a view não foi encontrada nos índices.
        /// </summary>
        public List<string> Caminhos { get; set; } = new();

        /// <summary>
        /// Total de caminhos encontrados. Sempre igual a Caminhos.Count.
        /// Exposto para binding direto na UI.
        /// </summary>
        public int TotalCaminhos => Caminhos.Count;

        /// <summary>
        /// Indica se a view não possui caminhos encontrados.
        /// Usado para exibir "Nenhum caminho encontrado" na UI.
        /// </summary>
        public bool SemCaminhos => Caminhos.Count == 0;
    }
}
