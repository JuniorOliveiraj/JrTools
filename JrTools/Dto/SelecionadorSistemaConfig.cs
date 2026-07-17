using System.Collections.Generic;

namespace JrTools.Dto
{
    public class SelecionadorSistemaConfig
    {
        public List<string> ServidoresRecentes { get; set; } = new();
        public List<CredencialRecente> CredenciaisRecentes { get; set; } = new();
        public Dictionary<string, ServidorHistorico> Servidores { get; set; } = new();
        public string UltimaPasta { get; set; } = string.Empty;
        public string UltimoServidor { get; set; } = string.Empty;
        public string UltimoSistema { get; set; } = string.Empty;
        public string UltimoAplicativo { get; set; } = "Runner";
        public string UltimoUsuario { get; set; } = string.Empty;
    }

    public class ServidorHistorico
    {
        public List<string> Sistemas { get; set; } = new();
        public string UltimoSistema { get; set; } = string.Empty;
        public Dictionary<string, SistemaHistorico> HistoricoSistemas { get; set; } = new();
    }

    public class SistemaHistorico
    {
        public string UltimoUsuario { get; set; } = string.Empty;
        public string UltimaSenha { get; set; } = string.Empty;
    }

    public class CredencialRecente
    {
        public string Usuario { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }
}
