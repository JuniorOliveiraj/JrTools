using JrTools.Services;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Implementação fake de <see cref="IProcessLauncher"/> para uso em testes.
    /// Captura os argumentos da última chamada a <see cref="Launch"/> sem lançar processos reais.
    /// </summary>
    public class FakeProcessLauncher : IProcessLauncher
    {
        /// <summary>Nome do executável passado na última chamada a <see cref="Launch"/>.</summary>
        public string? LastFileName { get; private set; }

        /// <summary>Argumentos passados na última chamada a <see cref="Launch"/>.</summary>
        public string? LastArguments { get; private set; }

        /// <summary>Número de vezes que <see cref="Launch"/> foi chamado.</summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public void Launch(string fileName, string arguments)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            CallCount++;
        }
    }
}
