using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AutoPrint.Services
{
    public class PrintQueueService
    {
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly HashSet<string> _seen = new();
        private readonly object _lock = new();

        public void Enqueue(string file)
        {
            lock (_lock)
            {
                if (_seen.Contains(file))
                    return;

                _seen.Add(file);
                _queue.Enqueue(file);
            }
        }

        public bool TryDequeue(out string file)
        {
            return _queue.TryDequeue(out file);
        }
    }
}