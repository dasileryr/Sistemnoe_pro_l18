using System;

namespace UserMonitorApp.Models
{
    public class ProcessLogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public bool WasBlocked { get; set; } = false;
    }
}

