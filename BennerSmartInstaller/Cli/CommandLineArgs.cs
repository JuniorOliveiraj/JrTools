using System;

namespace BennerSmartInstaller.Cli
{
    public class CommandLineArgs
    {
        public string CommandMode { get; set; } = "install";
        public string WebAppPath { get; set; }
        public string FilesArg { get; set; }
        /// <summary>Somente planeja (resolve dependências e lista a fila); não escreve na base.</summary>
        public bool DryRun { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}
