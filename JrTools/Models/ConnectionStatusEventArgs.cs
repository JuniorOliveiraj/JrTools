using System;
using JrTools.Enums;

namespace JrTools.Models
{
    /// <summary>
    /// Event arguments for connection status changes
    /// </summary>
    public class ConnectionStatusEventArgs : EventArgs
    {
        public ConnectionStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}