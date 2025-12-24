using System;

namespace UserMonitorApp.Models
{
    public class KeyLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Key { get; set; } = string.Empty;
        public bool IsSpecialKey { get; set; }
    }
}

