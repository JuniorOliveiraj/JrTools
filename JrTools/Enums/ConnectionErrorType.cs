namespace JrTools.Enums
{
    /// <summary>
    /// Specifies the type of connection error that occurred
    /// </summary>
    public enum ConnectionErrorType
    {
        None,
        DllNotFound,
        DllDependencyMissing,
        NetworkTimeout,
        ServerUnreachable,
        AuthenticationFailed,
        InvalidResponse,
        ValidationError
    }
}