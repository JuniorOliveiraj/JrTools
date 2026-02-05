using System;

namespace JrTools.Models
{
    /// <summary>
    /// Informações detalhadas de um processo
    /// </summary>
    public class ProcessInfo
    {
        public int PID { get; set; }
        public string ProcessName { get; set; }
        public string CommandLine { get; set; }
        public string MachineName { get; set; }
        public TimeSpan TotalProcessorTime { get; set; }
        public long WorkingSetBytes { get; set; }
        public double WorkingSetMB => Math.Round(WorkingSetBytes / 1024.0 / 1024.0, 2);
        public DateTime StartTime { get; set; }

        public ProcessInfo()
        {
            ProcessName = string.Empty;
            CommandLine = string.Empty;
            MachineName = Environment.MachineName;
        }
    }
}
