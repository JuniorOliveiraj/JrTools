using JrTools.Services;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Implementação fake de <see cref="IClipboardService"/> para uso em testes.
    /// Captura o texto definido via <see cref="SetText"/> sem acessar a área de transferência real.
    /// </summary>
    public class FakeClipboardService : IClipboardService
    {
        /// <summary>Texto definido na última chamada a <see cref="SetText"/>.</summary>
        public string? LastText { get; private set; }

        /// <summary>Número de vezes que <see cref="SetText"/> foi chamado.</summary>
        public int SetCallCount { get; private set; }

        /// <inheritdoc />
        public void SetText(string text)
        {
            LastText = text;
            SetCallCount++;
        }

        /// <inheritdoc />
        public string? GetText() => LastText;
    }
}
