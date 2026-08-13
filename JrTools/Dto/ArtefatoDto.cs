namespace JrTools.Dto
{
    public class ArtefatoDto
    {
        public string Identificador { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public string Guia { get; set; } = string.Empty; // ex: Menus, Pages, Views
        public string Camada { get; set; } = string.Empty; // ex: 20, 30, 40, 50
        public string CaminhoCompleto { get; set; } = string.Empty;
        public string Status { get; set; } = "Equal"; // "FileOnly" (Novo), "Diferent" (Modificado), "Equal" (Instalado)
        public bool IsSelecionado { get; set; }

        public bool IsPendente => Status.Equals("FileOnly", System.StringComparison.OrdinalIgnoreCase) ||
                                  Status.Equals("Diferent", System.StringComparison.OrdinalIgnoreCase) ||
                                  Status.Equals("Novo", System.StringComparison.OrdinalIgnoreCase) ||
                                  Status.Equals("Modificado", System.StringComparison.OrdinalIgnoreCase) ||
                                  Status.Equals("Pendente", System.StringComparison.OrdinalIgnoreCase);

        public string StatusFormatado
        {
            get
            {
                if (Status.Equals("FileOnly", System.StringComparison.OrdinalIgnoreCase)) return "Novo";
                if (Status.Equals("Diferent", System.StringComparison.OrdinalIgnoreCase)) return "Modificado";
                if (Status.Equals("Equal", System.StringComparison.OrdinalIgnoreCase)) return "Instalado";
                return Status;
            }
        }

        public string CamadaFormatada => $"Camada {Camada}";
        public string DescricaoCompleta => $"{Identificador} [{Guia}] [{StatusFormatado}] (Camada {Camada})";
    }
}
