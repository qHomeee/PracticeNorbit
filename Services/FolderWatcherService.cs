using System;
using System.IO;
using System.Linq;

namespace AutoPrint.Services
{
    public class FolderWatcherService
    {
        private readonly FileSystemWatcher _watcher;
        private readonly PrintQueueService _queue;
        private readonly string _path;

        public FolderWatcherService(string path, PrintQueueService queue)
        {
            _queue = queue;
            _path = path;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Created += OnCreated;
        }

        public void StartInitialScan()
        {
            var files = Directory.GetFiles(_path)
                .Where(IsSupported);

            foreach (var file in files)
            {
                _queue.Enqueue(file);
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsSupported(e.FullPath))
            {
                _queue.Enqueue(e.FullPath);
            }
        }

        private bool IsSupported(string file)
        {
            string ext = Path.GetExtension(file).ToLower();

            return ext is ".jpg" or ".jpeg" or ".png" or ".pdf"
                        or ".docx" or ".xlsx" or ".xls";
        }
    }
}