namespace JrTools.Services
{
    /// <summary>
    /// Abstração para lançamento de processos externos.
    /// Permite substituição por fake em testes.
    /// </summary>
    public interface IProcessLauncher
    {
        /// <summary>
        /// Lança um processo externo com o nome e argumentos informados.
        /// </summary>
        /// <param name="fileName">Nome ou caminho do executável.</param>
        /// <param name="arguments">Argumentos da linha de comando.</param>
        void Launch(string fileName, string arguments);
    }
}
