using System;
using System.Collections.Generic;

namespace JrTools.Dto
{
    public class BServerConfigDto
    {
        public string ServerAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 2000;
        public int TimeoutSeconds { get; set; } = 30;
        public List<string> RecentServers { get; set; } = new();
        public DateTime? LastConnectionAttempt { get; set; }
        public List<string> CachedSystems { get; set; } = new();
        public DateTime? SystemsCacheExpiry { get; set; }
    }
}