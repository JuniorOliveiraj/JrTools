namespace JrTools.Dto
{
    public class ArtefatoDto
    {
        public string Identificador { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public string Guia { get; set; } = string.Empty; // ex: Menus, Pages, Views
        public string Camada { get; set; } = string.Empty; // ex: 20, 30, 40, 50
        public string CaminhoCompleto { get; set; } = string.Empty;
        public string Status { get; set; } = "Não Verificado"; // "FileOnly" (Novo), "Diferent" (Modificado), "Equal" (Instalado), "Não Verificado"
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
                if (string.IsNullOrEmpty(Status) || Status.Equals("Não Verificado", System.StringComparison.OrdinalIgnoreCase)) return "Não Verificado";
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
