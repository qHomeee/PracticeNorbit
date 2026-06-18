using AutoPrint.Services;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPrint.ViewsModels
{
    internal class MainViewModel
    {
        private PrintQueueService _queue;
        private PrintWorker _worker;
        private FolderWatcherService _watcher;
        private PrintService _printer;
        private LogService _log;


        string folderPath = @"C:\PrintTest";
        string printerName = PrinterSettings.InstalledPrinters.Cast<string>().First();
    }
}
