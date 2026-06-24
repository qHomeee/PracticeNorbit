using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Drawing.Printing;
using Microsoft.Win32;
using AutoPrint.Services;
using AutoPrint.Models;
using System.Collections.Generic;

namespace AutoPrint
{
    public partial class MainWindow : Window
    {
        private PrintQueueService _queue;
        private PrintWorker _worker;
        private PrintService _printer;
        private LogService _log;

        private readonly ConfigService _configService = new();
        private AppConfig _config;

        private ObservableCollection<string> _folders = new();
        private List<FolderWatcherService> _watchers = new();
        private bool _isRunning = false;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += Window_Closed;
            FoldersList.ItemsSource = _folders;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            LoadPrinters();
            RestoreAndStart();
        }

        private void LoadConfig()
        {
            _config = _configService.Load() ?? new AppConfig();
            if (_config.WatchFolders != null)
            {
                foreach (var folder in _config.WatchFolders)
                    if (!_folders.Contains(folder))
                        _folders.Add(folder);
            }
        }

        private void LoadPrinters()
        {
            PrinterBox.Items.Clear();
            foreach (string p in PrinterSettings.InstalledPrinters)
                PrinterBox.Items.Add(p);

            if (!string.IsNullOrWhiteSpace(_config?.PrinterName) && PrinterBox.Items.Contains(_config.PrinterName))
                PrinterBox.SelectedItem = _config.PrinterName;
            else if (PrinterBox.Items.Count > 0)
                PrinterBox.SelectedIndex = 0;
        }

        private void RestoreAndStart()
        {
            if (_folders.Count == 0)
                return;

            if (!string.IsNullOrWhiteSpace(_config?.PrinterName))
                PrinterBox.SelectedItem = _config.PrinterName;

            StartSystem();
        }

        private void StartSystem()
        {
            if (_isRunning) return;

            if (!_isInitialized)
            {
                _queue = new PrintQueueService();
                _log = new LogService();
                _isInitialized = true;
            }

            string printer = PrinterBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(printer))
            {
                MessageBox.Show("Выберите принтер", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_printer == null || _printer.PrinterName != printer)
            {
                _printer = new PrintService(printer);
                _worker?.Stop();
                _worker = new PrintWorker(_queue, _printer, _log);
                _worker.Start();
            }
            else if (_worker == null)
            {
                _worker = new PrintWorker(_queue, _printer, _log);
                _worker.Start();
            }

            foreach (var folder in _folders)
            {
                if (!_watchers.Any(w => w.Path == folder))
                {
                    var watcher = new FolderWatcherService(folder, _queue);
                    watcher.StartInitialScan();
                    _watchers.Add(watcher);
                }
            }

            _isRunning = true;
            _log.Info("Auto monitoring started");
            LogBox.Items.Add("Monitoring started");
        }

        private void StopSystem()
        {
            if (!_isRunning) return;

            _worker?.Stop();
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
            _isRunning = false;
            _log.Info("Auto monitoring stopped");
            LogBox.Items.Add("Monitoring stopped");
        }

        private void AddFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
            {
                MessageBox.Show("Укажите существующую папку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_folders.Contains(path))
            {
                MessageBox.Show("Эта папка уже добавлена", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _folders.Add(path);
            _config.WatchFolders = _folders.ToList();
            _configService.Save(_config);

            if (_isRunning)
            {
                var watcher = new FolderWatcherService(path, _queue);
                watcher.StartInitialScan();
                _watchers.Add(watcher);
                _log.Info($"Added folder: {path}");
                LogBox.Items.Add($"Added folder: {path}");
            }
            else
            {
                // Если система ещё не запущена, запускаем её
                StartSystem();
            }
        }

        private void RemoveFolder(string path)
        {
            if (!_folders.Contains(path)) return;

            _folders.Remove(path);
            _config.WatchFolders = _folders.ToList();
            _configService.Save(_config);

            var watcher = _watchers.FirstOrDefault(w => w.Path == path);
            if (watcher != null)
            {
                watcher.Dispose();
                _watchers.Remove(watcher);
                _log.Info($"Removed folder: {path}");
                LogBox.Items.Add($"Removed folder: {path}");
            }

            if (_folders.Count == 0 && _isRunning)
                StopSystem();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
                NewFolderBox.Text = dialog.FolderName;
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = NewFolderBox.Text.Trim();
            AddFolder(path);
            NewFolderBox.Clear();
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            string selected = FoldersList.SelectedItem as string;
            if (!string.IsNullOrEmpty(selected))
                RemoveFolder(selected);
        }

        private void PrinterBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PrinterBox.SelectedItem != null)
            {
                _config.PrinterName = PrinterBox.SelectedItem.ToString();
                _configService.Save(_config);

                // Если система уже запущена, перезапускаем с новым принтером
                if (_isRunning)
                {
                    StopSystem();
                    StartSystem();
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopSystem();
            _config.WatchFolders = _folders.ToList();
            _configService.Save(_config);
        }
    }
}