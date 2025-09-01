using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class PageEspesificosDataObject
    {
        /// <summary>
        /// Projeto específico selecionado na ComboBox
        /// </summary>
        public string Projeto { get; set; }

        // Opções de Compilação
        public bool BaixarBinario { get; set; }
        public bool CriarAtalho { get; set; }
        public bool CompilarEspecificos { get; set; }
        public bool CriarAplicacaoIIS { get; set; }
        public bool RestaurarWebApp { get; set; }

        // Configuração da Aplicação Web
        public string EnderecoServidor { get; set; }
        public string UsuarioInterno { get; set; }
        public string SenhaInterno { get; set; }

        // Outras Informações
        public string Site { get; set; }
        public string NomeAplicacao { get; set; }
        public string Pool { get; set; }
        public string NumeroProvedores { get; set; }
        public string NomeSistemaBenner { get; set; }
    }
}
