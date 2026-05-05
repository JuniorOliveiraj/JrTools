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
        /// Branch selecionada
        /// </summary>
        public string Branch { get; set; }
       /// <summary>
       /// Flag para Pegar binários atualizados VAPL
       /// </summary>
        public bool AtualizarBinarios { get; set; }
        /// <summary>
        /// Flag para Buildar projeto 
        /// </summary>
        public bool BuildarProjeto { get; set; }

        /// <summary>
        /// Dar um git pull na Branch selecionada
        /// </summary>
        public bool AtualizarBranch { get; set; }
        
        /// <summary>
        /// Quando selecionado o campo Outros no select deve ser preenchida a branch para se posicionar
        /// </summary>
        public string? BranchEspecificaDeTrabalho { get; set; }

        /// <summary>
        ///  Matar processo runner.exe antes de iniciar o build
        /// </summary>
        public bool RunnerFechado { get; set; }
        /// <summary>
        ///  Matar processo builder.exe antes de iniciar o build
        /// </summary>
        public bool BuilderFechado { get; set; }

        /// <summary>
        /// Matar processo provider.exe antes de iniciar o build
        /// </summary>
        public bool ProviderFechado { get; set; }
        /// <summary>
        /// Quando selecionado o campo Outros no select deve ser preenchida a tag do git para se posicionar
        /// </summary>
        public string? TagEspecificaDeTrabalho { get; set; }


    }
}
