namespace JrTools.Dto
{
    public class ConfiguracaoRelatoriosRh
    {
        public string CaminhoRelatorios { get; set; } = string.Empty;
        public string Servidor         { get; set; } = string.Empty;
        public string Sistema          { get; set; } = "rh";
        public string Usuario          { get; set; } = string.Empty;
        public string Senha            { get; set; } = string.Empty;
    }
}
