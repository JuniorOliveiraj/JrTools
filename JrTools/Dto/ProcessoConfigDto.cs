using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class ProcessoConfigDto
    {
        /// <summary>
        /// Nome do processo
        /// </summary>
        public string Nome { get; set; }
        /// <summary>
        /// Indica se ele vai iniciar ativo
        /// </summary>
        public bool AtivoPorPadrao { get; set; }

        public ProcessoConfigDto(string nome, bool ativoPorPadrao = true)
        {
            Nome = nome;
            AtivoPorPadrao = ativoPorPadrao;
        }
    }
}
