namespace JrTools.Models
{
    /// <summary>
    /// Event arguments para eventos de processo (criação/término)
    /// </summary>
    public class ProcessEventArgs : System.EventArgs
    {
        public string ProcessName { get; }
        public int ProcessId { get; }

        public ProcessEventArgs(string processName, int processId)
        {
            ProcessName = processName;
            ProcessId = processId;
        }
    }
}
