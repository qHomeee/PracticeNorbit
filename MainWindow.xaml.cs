using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AutoPrint.Models;
using AutoPrint.Services;
using Microsoft.Win32;

namespace AutoPrint
{
    public partial class MainWindow : Window
    {
        private const int MaxLogEntries = 500;

        private PrintQueueService? _queue;
        private PrintWorker? _worker;
        private PrintService? _printer;
        private LogService? _log;

        private readonly ConfigService _configService = new();
        private AppConfig _config = new();

        private readonly ObservableCollection<string> _folders = new();
        private readonly List<FolderWatcherService> _watchers = new();
        private bool _isRunning;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            FoldersList.ItemsSource = _folders;
            UpdateStatusUI(false);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            LoadPrinters();
        }

        private void LoadConfig()
        {
            _config = _configService.Load();
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

            if (!string.IsNullOrWhiteSpace(_config.PrinterName) && PrinterBox.Items.Contains(_config.PrinterName))
                PrinterBox.SelectedItem = _config.PrinterName;
            else if (PrinterBox.Items.Count > 0)
                PrinterBox.SelectedIndex = 0;
        }

        private void StartSystem()
        {
            if (_isRunning) return;

            string? printer = PrinterBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(printer))
            {
                MessageBox.Show("Выберите принтер перед запуском", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_folders.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну папку для мониторинга", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _log = new LogService();
                _queue = new PrintQueueService();
                _printer = new PrintService(printer);
                _worker = new PrintWorker(_queue, _printer, _log);
                _worker.OnLog += msg => Dispatcher.Invoke(() => AddLogEntry(msg));
                _worker.Start();

                foreach (var folder in _folders)
                    AddWatcher(folder);

                _isRunning = true;
                UpdateStatusUI(true);
                _log.Info("Мониторинг запущен");
                AddLogEntry("Мониторинг запущен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                CleanupSystem();
            }
        }

        private void StopSystem()
        {
            if (!_isRunning) return;

            try
            {
                _worker?.Stop();
                foreach (var watcher in _watchers)
                    watcher.Dispose();
                _watchers.Clear();
                _queue?.Clear();

                _log?.Info("Мониторинг остановлен");
                AddLogEntry("Мониторинг остановлен");
            }
            catch (Exception ex)
            {
                _log?.Error($"Ошибка при остановке: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                UpdateStatusUI(false);
            }
        }

        private void CleanupSystem()
        {
            try
            {
                _worker?.Stop();
                foreach (var watcher in _watchers)
                    watcher.Dispose();
                _watchers.Clear();
            }
            catch { }

            _worker = null;
            _queue = null;
            _printer = null;
            _isRunning = false;
            UpdateStatusUI(false);
        }

        private void AddWatcher(string folder)
        {
            if (_watchers.Any(w => w.Path == folder)) return;
            if (_queue == null || _log == null) return;

            try
            {
                var watcher = new FolderWatcherService(folder, _queue, _log);
                watcher.StartInitialScan();
                _watchers.Add(watcher);
                _log.Info($"Добавлена папка: {folder}");
                AddLogEntry($"Папка добавлена: {System.IO.Path.GetFileName(folder)}");
            }
            catch (Exception ex)
            {
                _log.Error($"Не удалось добавить папку {folder}: {ex.Message}");
                AddLogEntry($"Ошибка добавления папки: {ex.Message}");
            }
        }

        private void AddFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
            {
                MessageBox.Show("Укажите существующую папку", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_folders.Contains(path))
            {
                MessageBox.Show("Эта папка уже добавлена", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _folders.Add(path);
            _config.WatchFolders = _folders.ToList();
            _configService.Save(_config);

            if (_isRunning)
                AddWatcher(path);
            else
                AddLogEntry($"Папка добавлена: {System.IO.Path.GetFileName(path)} (нажмите Старт)");
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
                _log?.Info($"Удалена папка: {path}");
                AddLogEntry($"Папка удалена: {System.IO.Path.GetFileName(path)}");
            }
        }

        private void UpdateStatusUI(bool running)
        {
            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            PrinterBox.IsEnabled = !running;
            BrowseFolderButton.IsEnabled = true;
            AddFolderButton.IsEnabled = true;
            RemoveFolderButton.IsEnabled = true;

            if (running)
            {
                StatusText.Text = "● Работает";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StatusText.Text = "○ Остановлено";
                StatusText.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void AddLogEntry(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AddLogEntry(message));
                return;
            }

            string timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogBox.Items.Add(timestamped);

            while (LogBox.Items.Count > MaxLogEntries)
                LogBox.Items.RemoveAt(0);

            LogBox.ScrollIntoView(LogBox.Items[^1]);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartSystem();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
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
            string? selected = FoldersList.SelectedItem as string;
            if (!string.IsNullOrEmpty(selected))
                RemoveFolder(selected);
        }

        private void PrinterBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PrinterBox.SelectedItem != null)
            {
                _config.PrinterName = PrinterBox.SelectedItem.ToString()!;
                _configService.Save(_config);

                if (_isRunning)
                {
                    AddLogEntry("Смена принтера требует перезапуска. Нажмите Стоп, затем Старт.");
                }
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            StopSystem();
            _config.WatchFolders = _folders.ToList();
            _configService.Save(_config);
        }
    }
}
