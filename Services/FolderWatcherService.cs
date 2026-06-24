using System;
using System.IO;
using System.Linq;

namespace AutoPrint.Services
{
    public class FolderWatcherService : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private readonly PrintQueueService _queue;
        private readonly string _path;
        private readonly LogService _log;
        private bool _disposed;

        public string Path => _path;

        public FolderWatcherService(string path, PrintQueueService queue, LogService log)
        {
            _queue = queue;
            _path = path;
            _log = log;

            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                _log.Error($"Не удалось создать папку {_path}: {ex.Message}");
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
                };

                _watcher.Created += OnCreated;
                _watcher.Renamed += OnRenamed;
                _watcher.Error += OnError;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _log.Error($"Не удалось запустить наблюдение за {_path}: {ex.Message}");
                _watcher?.Dispose();
                _watcher = null;
            }
        }

        public void StartInitialScan()
        {
            try
            {
                if (!Directory.Exists(_path)) return;

                var files = Directory.GetFiles(_path)
                    .Where(IsSupported);

                foreach (var file in files)
                {
                    try
                    {
                        _queue.Enqueue(file);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Ошибка добавления файла {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Ошибка сканирования папки {_path}: {ex.Message}");
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            TryEnqueue(e.FullPath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            TryEnqueue(e.FullPath);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _log.Error($"FileSystemWatcher error in {_path}: {e.GetException().Message}");
            TryRestartWatcher();
        }

        private void TryEnqueue(string fullPath)
        {
            try
            {
                if (IsSupported(fullPath) && File.Exists(fullPath))
                    _queue.Enqueue(fullPath);
            }
            catch (Exception ex)
            {
                _log.Error($"Ошибка обработки файла {fullPath}: {ex.Message}");
            }
        }

        private void TryRestartWatcher()
        {
            try
            {
                if (_watcher != null && !_disposed)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }

                _watcher = new FileSystemWatcher(_path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
                };
                _watcher.Created += OnCreated;
                _watcher.Renamed += OnRenamed;
                _watcher.Error += OnError;
                _watcher.EnableRaisingEvents = true;
                _log.Info($"Watcher перезапущен для {_path}");
            }
            catch (Exception ex)
            {
                _log.Error($"Не удалось перезапустить watcher для {_path}: {ex.Message}");
            }
        }

        private static bool IsSupported(string file)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                return ext is ".jpg" or ".jpeg" or ".png" or ".pdf"
                            or ".docx" or ".xlsx" or ".xls" or ".xlsm" or ".xlsb";
            }
            catch
            {
                return false;
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= OnCreated;
                    _watcher.Renamed -= OnRenamed;
                    _watcher.Error -= OnError;
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _watcher?.Dispose();
        }
    }
}
