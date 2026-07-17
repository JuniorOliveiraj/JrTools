namespace JrTools.Enums
{
    /// <summary>
    /// Represents the current status of a connection
    /// </summary>
    public enum ConnectionStatus
    {
        Idle,
        Connecting,
        Connected,
        Disconnected,
        Error,
        DllUnavailable
    }
}