namespace JrTools.Dto
{
    public class BinarioDelphiDetalhe
    {
        /// <summary>Caminho completo do arquivo.</summary>
        public string CaminhoCompleto { get; init; } = string.Empty;

        /// <summary>Versão do arquivo (FileVersionInfo.FileVersion).</summary>
        public string? VersaoArquivo { get; init; }

        /// <summary>Versão do produto (FileVersionInfo.ProductVersion).</summary>
        public string? VersaoProduto { get; init; }

        /// <summary>Descrição do arquivo (FileVersionInfo.FileDescription).</summary>
        public string? DescricaoArquivo { get; init; }

        /// <summary>Empresa (FileVersionInfo.CompanyName).</summary>
        public string? Empresa { get; init; }

        /// <summary>Indica se ocorreu erro ao carregar os detalhes.</summary>
        public bool ErroAoCarregar { get; init; }

        /// <summary>Mensagem de erro, se houver.</summary>
        public string? MensagemErro { get; init; }
    }
}
