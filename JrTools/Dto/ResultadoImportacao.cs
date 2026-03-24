namespace JrTools.Dto
{
    public class ResultadoImportacao
    {
        public string Saida { get; init; }
        public int ExitCode { get; init; }
        public bool Sucesso => ExitCode == 0;
    }
}
