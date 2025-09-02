using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Utils
{
    public class FluxoException : Exception
    {
        public FluxoException(string mensagem) : base(mensagem) { }
    }
}
