using System.Collections.Generic;

namespace JrTools.Dto
{
    /// <summary>
    /// Persistência dos processos adicionados manualmente em "Fechar Processos"
    /// (além dos 4 padrão hardcoded), incluindo o estado ligado/desligado do toggle.
    /// </summary>
    public class ProcessoCustomizadoConfig
    {
        public List<ProcessoCustomizadoItem> Processos { get; set; } = new();
    }

    public class ProcessoCustomizadoItem
    {
        public string Nome { get; set; } = string.Empty;
        public bool Habilitado { get; set; }
    }
}
