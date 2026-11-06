using System;

namespace BennerSmartInstaller.Cli
{
    public class CommandLineArgs
    {
        public string CommandMode { get; set; } = "install";
        public string WebAppPath { get; set; }
        public string FilesArg { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}
