using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class DadosPessoaisDataObject
    {
        public string LoginRhWeb { get; set; }
        public string SenhaRhWeb { get; set; }

        public string LoginDevSite { get; set; }
        public string SenhaDevSite { get; set; }

        public string? TokenDevSite { get; set; }
        public string? TokenRhWeb { get; set; }
        public DateTime? TokenRhExpiraEm { get; set; }


        public string? ApiGemini { get; set; }
        public string? ApiToggl { get; set; }
    }
}
