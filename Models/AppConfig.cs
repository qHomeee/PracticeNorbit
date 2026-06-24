using System.Collections.Generic;

namespace AutoPrint.Models
{
    public class AppConfig
    {
        public List<string> WatchFolders { get; set; } = new();
        public string? PrinterName { get; set; }
    }
}