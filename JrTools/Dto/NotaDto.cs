using System;
using System.Text.RegularExpressions;

namespace JrTools.Dto
{
    public class NotaDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Titulo { get; set; } = "";
        public string Conteudo { get; set; } = "";
        public string Topico { get; set; } = "Geral";
        public DateTime CriadoEm { get; set; } = DateTime.Now;
        public DateTime EditadoEm { get; set; } = DateTime.Now;

        public string EditadoEmFormatado => EditadoEm.ToString("dd/MM/yy");

        public string PreviewConteudo
        {
            get
            {
                var texto = Regex.Replace(Conteudo, @"[#*`_\[\]>~]", "").Trim();
                texto = Regex.Replace(texto, @"\n+", " ").Trim();
                return texto.Length > 100 ? texto[..100] + "..." : texto;
            }
        }
    }
}
