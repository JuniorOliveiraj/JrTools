namespace JrTools.Models
{
    /// <summary>
    /// Linha do grid de seleção do modal "Adicionar Processo" — um processo distinto
    /// rodando na máquina agora (agrupado por nome, já que o app monitora/mata por nome,
    /// não por PID individual). Não é persistido.
    /// </summary>
    public class ProcessoDisponivel
    {
        public string Nome { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public string? CaminhoExecutavel { get; set; }
    }
}
