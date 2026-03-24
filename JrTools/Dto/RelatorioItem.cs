using JrTools.Enums;

namespace JrTools.Dto
{
    public class RelatorioItem
    {
        public string Nome { get; set; }
        public string Caminho { get; set; }
        public string Hash { get; set; }
        public StatusRelatorio Status { get; set; }
        public bool Selecionado { get; set; }

        public string StatusTexto => Status switch
        {
            StatusRelatorio.Novo => "Novo",
            StatusRelatorio.Diferente => "Alterado",
            StatusRelatorio.Atualizado => "Atualizado",
            _ => ""
        };

        public string StatusCor => Status switch
        {
            StatusRelatorio.Novo => "#2196F3",
            StatusRelatorio.Diferente => "#FF9800",
            StatusRelatorio.Atualizado => "#4CAF50",
            _ => "#9E9E9E"
        };
    }
}
