using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AutoPrint.Services
{
    public class PrintWorker
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 2000;
        private const int IdleDelayMs = 500;
        private const int FileReadyTimeoutMs = 10000;
        private const int FileReadyPollMs = 500;

        private readonly PrintQueueService _queue;
        private readonly PrintService _printer;
        private readonly LogService _log;
        private CancellationTokenSource? _cts;
        private Task? _task;

        public event Action<string>? OnLog;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public PrintWorker(PrintQueueService queue, PrintService printer, LogService log)
        {
            _queue = queue;
            _printer = printer;
            _log = log;
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _task = Task.Run(() => Loop(token), token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _task?.Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException)
            {
            }
            _cts?.Dispose();
            _cts = null;
            _task = null;
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out var file))
                {
                    await ProcessFile(file, token);
                }
                else
                {
                    await Task.Delay(IdleDelayMs, token).ConfigureAwait(false);
                }
            }
        }

        private async Task ProcessFile(string file, CancellationToken token)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await WaitFileReady(file, token);
                    token.ThrowIfCancellationRequested();

                    _printer.PrintFile(file);
                    SafeMove(file, GetPrintedFolder());
                    _log.Info("Printed: " + file);
                    OnLog?.Invoke("Напечатано: " + Path.GetFileName(file));
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Ошибка (попытка {attempt}/{MaxRetries}): {Path.GetFileName(file)} -> {ex.Message}";
                    _log.Error(errorMsg);
                    OnLog?.Invoke(errorMsg);

                    if (attempt < MaxRetries)
                    {
                        try
                        {
                            await Task.Delay(RetryDelayMs * attempt, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            }

            try
            {
                SafeMove(file, GetErrorFolder());
                string msg = $"Файл перемещён в Error: {Path.GetFileName(file)}";
                _log.Error(msg);
                OnLog?.Invoke(msg);
            }
            catch (Exception moveEx)
            {
                string msg = $"Не удалось переместить файл {Path.GetFileName(file)}: {moveEx.Message}";
                _log.Error(msg);
                OnLog?.Invoke(msg);
            }
        }

        private async Task WaitFileReady(string file, CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(FileReadyTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var s = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(FileReadyPollMs, token).ConfigureAwait(false);
                }
            }
            throw new IOException("File locked too long");
        }

        private static void SafeMove(string file, string folder)
        {
            if (!File.Exists(file)) return;

            Directory.CreateDirectory(folder);
            string dest = Path.Combine(folder, Path.GetFileName(file));

            if (File.Exists(dest))
            {
                string uniqueDest = Path.Combine(folder,
                    Path.GetFileNameWithoutExtension(file) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + Path.GetExtension(file));
                dest = uniqueDest;
            }

            File.Move(file, dest);
        }

        private static string GetPrintedFolder() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Printed");
        private static string GetErrorFolder() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Error");
    }
}
