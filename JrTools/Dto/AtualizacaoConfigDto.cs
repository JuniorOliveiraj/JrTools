using System;
using System.Text.Json.Serialization;
using JrTools.Enums;

namespace JrTools.Dto
{
    public class AtualizacaoConfigDto
    {
        public DateTime UltimaVerificacaoUtc { get; set; } = DateTime.MinValue;
        public string UltimaTagModalExibida { get; set; } = string.Empty;
        public bool VerificarAutomaticamente { get; set; } = true;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PeriodicidadeAtualizacao Periodicidade { get; set; } = PeriodicidadeAtualizacao.Diario;
    }
}
