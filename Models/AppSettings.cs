using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPrint.Models
{
    internal class AppSettings
    {
        public string WatchFolder { get; set; } = @"C:\PrintTest";
        public string PrinterName { get; set; } = "";
    }
}
