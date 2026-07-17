using JrTools.Flows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    /// <summary>
    /// Informações de progresso do espelhamento
    /// </summary>
    public class ProgressoEspelhamento
    {
        public FaseEspelhamento Fase { get; set; }
        public double Percentual { get; set; }
        public int ArquivosProcessados { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosCopiadados { get; set; }
        public string Status { get; set; } = "";
        public string Detalhes { get; set; } = "";
        public string Mensagem { get; set; } = "";
    }

    /// <summary>
    /// Fases do processo de espelhamento
    /// </summary>
    public enum FaseEspelhamento
    {
        Analise,
        Limpeza,
        Copia,
        MonitoramentoContinuo
    }


    public enum ModoEspelhamento
    {
        Espelhar,    // Destino fica idêntico: copia novos/alterados E apaga extras
        Sincronizar  // Apenas copia novos/alterados; não apaga arquivos extras no destino
    }

    /// <summary>
    /// Representa um perfil de espelhamento de diretórios
    /// </summary>
    public class PerfilEspelhamento
    {
        public string Nome { get; set; } = "";
        public string DiretorioOrigem { get; set; } = "";
        public string DiretorioDestino { get; set; } = "";
        public ModoEspelhamento Modo { get; set; } = ModoEspelhamento.Espelhar;

        public string ModoDisplay => Modo == ModoEspelhamento.Sincronizar ? "Sincronizar" : "Espelhar";
    }

    /// <summary>
    /// Informações de progresso do espelhamento
    /// </summary>
    public class ProgressoEspelhamentoCopy
    {
        public FaseEspelhamento Fase { get; set; }
        public double Percentual { get; set; }
        public int ArquivosProcessados { get; set; }
        public int TotalArquivos { get; set; }
        public int ArquivosCopiadados { get; set; }
        public string Status { get; set; } = "";
        public string Detalhes { get; set; } = "";
        public string Mensagem { get; set; } = "";
    }
}
