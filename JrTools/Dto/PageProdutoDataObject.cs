using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class PageProdutoDataObject
    {
        /// <summary>
        /// Breach selecionada
        /// </summary>
        public string Breach { get; set; }
       /// <summary>
       /// Flag para Pegar binarios atualizados VAPL
       /// </summary>
        public bool AtualizarBinarios { get; set; }
        /// <summary>
        /// Flag para Buildar projeto 
        /// </summary>
        public bool BuildarProjeto { get; set; }

        /// <summary>
        /// Dar um git pull na Breach selecionada
        /// </summary>
        public bool AtualizarBreach { get; set; }
        
        /// <summary>
        /// Quando selecionado o campo Outros no select deve ser preenchida a breanch para se posicionar
        /// </summary>
        public string? BreachEspesificaDeTrabalho { get; set; }

        /// <summary>
        ///  matar rpocesso runner.exe antes de iniciar o build
        /// </summary>
        public bool RunnerFechado { get; set; }
        /// <summary>
        ///  Matar processo builder.exe antes de iniciar o build
        /// </summary>
        public bool BuilderFechado { get; set; }

        /// <summary>
        /// Matar processo privider.exe antes de iniciar o build
        /// </summary>
        public bool PrividerFechado { get; set; }


    }
}
