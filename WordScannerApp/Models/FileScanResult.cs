using System;
using System.Collections.Generic;

namespace WordScannerApp.Models
{
    public class FileScanResult
    {
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public Dictionary<string, int> WordCounts { get; set; } = new Dictionary<string, int>();
        public int TotalReplacements { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
    }
}

