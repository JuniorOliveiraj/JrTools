using System;
using JrTools.Enums;

namespace JrTools.Models
{
    /// <summary>
    /// Represents the result of a BServer connection attempt
    /// </summary>
    public class ConnectionResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan ConnectionTime { get; set; }
        public ConnectionErrorType ErrorType { get; set; }
        public string[] AvailableSystems { get; set; } = Array.Empty<string>();
    }
}