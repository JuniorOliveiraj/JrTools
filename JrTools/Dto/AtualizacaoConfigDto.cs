using System;

namespace JrTools.Dto
{
    public class AtualizacaoConfigDto
    {
        public DateTime UltimaVerificacaoUtc { get; set; } = DateTime.MinValue;
        public string UltimaTagModalExibida { get; set; } = string.Empty;
        public bool VerificarAutomaticamente { get; set; } = true;
        public string Periodicidade { get; set; } = "Diario";
    }
}
