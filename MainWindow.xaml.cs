using System;
using System.Linq;
using System.Windows;
using System.Drawing.Printing;
using Microsoft.Win32;
using AutoPrint.Services;

namespace AutoPrint
{
    public partial class MainWindow : Window
    {
        private PrintQueueService _queue;
        private PrintWorker _worker;
        private FolderWatcherService _watcher;
        private PrintService _printer;
        private LogService _log;

        private string _folder;

        public MainWindow()
        {
            InitializeComponent();

            LoadPrinters();
        }

        private void LoadPrinters()
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
                PrinterBox.Items.Add(p);

            PrinterBox.SelectedIndex = 0;
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() == true)
            {
                _folder = dialog.FolderName;
                FolderBox.Text = _folder;

                StartSystem(); // 👈 ВОТ ГЛАВНОЕ ИЗМЕНЕНИЕ
            }
        }

        private void StartSystem()
        {
            if (string.IsNullOrWhiteSpace(_folder))
                return;

            _queue = new PrintQueueService();
            _log = new LogService();

            string printer = PrinterBox.SelectedItem.ToString();

            _printer = new PrintService(printer);
            _worker = new PrintWorker(_queue, _printer, _log);

            _watcher = new FolderWatcherService(_folder, _queue);
            _watcher.StartInitialScan();

            _worker.Start();

            _log.Info("Auto monitoring started");

            LogBox.Items.Add("Monitoring started");
        }
    }
}