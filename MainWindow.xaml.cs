using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Drawing.Printing;
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
        private string _printerName;

        public MainWindow()
        {
            InitializeComponent();

            LoadPrinters();
        }

        private void LoadPrinters()
        {
            foreach (string p in PrinterSettings.InstalledPrinters)
            {
                PrinterBox.Items.Add(p);
            }

            PrinterBox.SelectedIndex = 0;
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _folder = dialog.SelectedPath;
                FolderBox.Text = _folder;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folder))
            {
                System.Windows.MessageBox.Show("Выбери папку");
                return;
            }

            _printerName = PrinterBox.SelectedItem.ToString();

            _queue = new PrintQueueService();
            _log = new LogService();

            _printer = new PrintService(_printerName);
            _worker = new PrintWorker(_queue, _printer, _log);

            _watcher = new FolderWatcherService(_folder, _queue);
            _watcher.StartInitialScan();

            _worker.Start();

            _log.Info("Started");

            LogBox.Items.Add("System started");
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _worker?.Stop();
            LogBox.Items.Add("Stopped");
        }
    }
}