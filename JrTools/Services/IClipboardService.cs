namespace JrTools.Services
{
    /// <summary>
    /// Abstração para acesso à área de transferência.
    /// Permite substituição por fake em testes (evita dependência de WinRT/UI thread).
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>Define o texto na área de transferência.</summary>
        void SetText(string text);

        /// <summary>Obtém o texto atual da área de transferência, ou null se não houver texto.</summary>
        string? GetText();
    }
}
