using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class SolucaoInformacoesDto
    {
        public string Nome { get; set; }
        public string Caminho { get; set; }
    }

    public class PastaInformacoesDto
    {
        public string Nome { get; set; }
        public string Caminho { get; set; }
    }

    public class PastaBinariosDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Caminho { get; set; } = string.Empty;
        public string Versao { get; set; } = string.Empty;
        public string NomeDisplay => string.IsNullOrWhiteSpace(Versao) ? Nome : $"{Versao}  —  {Nome}";
    }
}
