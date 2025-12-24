using System.Collections.Generic;

namespace UserMonitorApp.Models
{
    public class MonitorSettings
    {
        public bool EnableStatistics { get; set; } = true;
        public bool EnableModeration { get; set; } = false;
        public string ReportPath { get; set; } = "C:\\UserMonitorReports";
        public List<string> ForbiddenWords { get; set; } = new List<string>();
        public List<string> ForbiddenPrograms { get; set; } = new List<string>();
    }
}

