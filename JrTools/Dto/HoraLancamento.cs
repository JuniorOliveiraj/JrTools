using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class HoraLancamento
    {
        public DateTime? Data { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFim { get; set; }
        public double? TotalHoras { get; set; }
        public string? Descricao { get; set; }
        public string? Projeto { get; set; }
    }
}
