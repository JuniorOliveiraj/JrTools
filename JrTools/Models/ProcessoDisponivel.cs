using Microsoft.UI.Xaml.Media;

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

        /// <summary>Ícone do executável, em PNG — extraído numa thread de background (não é
        /// seguro criar tipos de imagem do WinUI fora da UI thread). Decodificado como <see
        /// cref="Icone"/> só depois, na UI thread, antes do grid renderizar.</summary>
        public byte[]? IconePng { get; set; }
        public ImageSource? Icone { get; set; }
    }
}
