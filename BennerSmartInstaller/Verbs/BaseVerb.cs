using CommandLine;

namespace BennerSmartInstaller.Verbs
{
    public abstract class BaseVerb
    {
        [Option('v', "verbose", Default = false, HelpText = "Exibir detalhes de erros e stack trace completo.")]
        public bool Verbose { get; set; }

        public abstract void Execute();
    }
}
