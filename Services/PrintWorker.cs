using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AutoPrint.Services
{
    public class PrintWorker
    {
        private readonly PrintQueueService _queue;
        private readonly PrintService _printer;
        private readonly LogService _log;
        private CancellationTokenSource _cts;

        public PrintWorker(PrintQueueService queue, PrintService printer, LogService log)
        {
            _queue = queue;
            _printer = printer;
            _log = log;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => Loop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out var file))
                {
                    try
                    {
                        await WaitFileReady(file);
                        _printer.PrintFile(file);
                        Move(file, @"C:\PrintTest\Printed");
                        _log.Info("Printed: " + file);
                    }
                    catch (Exception ex)
                    {
                        Move(file, @"C:\PrintTest\Error");
                        _log.Error($"Failed: {file} -> {ex.Message}");
                    }
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }

        private async Task WaitFileReady(string file)
        {
            int tries = 10;
            while (tries-- > 0)
            {
                try
                {
                    using var s = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
                    return;
                }
                catch
                {
                    await Task.Delay(500);
                }
            }
            throw new Exception("File locked too long");
        }

        private void Move(string file, string folder)
        {
            Directory.CreateDirectory(folder);
            string dest = Path.Combine(folder, Path.GetFileName(file));
            if (File.Exists(dest))
                File.Delete(dest);
            File.Move(file, dest);
        }
    }
}