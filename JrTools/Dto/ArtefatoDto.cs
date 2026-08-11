namespace JrTools.Dto
{
    public class ArtefatoDto
    {
        public string Identificador { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public string Guia { get; set; } = string.Empty; // ex: Menus, Pages, Views
        public string Camada { get; set; } = string.Empty; // ex: 20, 30, 40, 50
        public string CaminhoCompleto { get; set; } = string.Empty;
        public bool IsSelecionado { get; set; }

        public string CamadaFormatada => $"Camada {Camada}";
        public string DescricaoCompleta => $"{Identificador} [{Guia}] (Camada {Camada})";
    }
}
