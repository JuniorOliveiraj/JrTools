using System;

namespace JrTools.Dto
{
    public class HoraLancamento
    {
        public long Id { get; set; }
        public DateTime? Data { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFim { get; set; }
        public double? TotalHoras { get; set; }
        public string? Descricao { get; set; }
        public string? Projeto { get; set; }
    }
}
