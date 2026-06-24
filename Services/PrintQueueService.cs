using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AutoPrint.Services
{
    public class PrintQueueService
    {
        private const int MaxSeenEntries = 10000;

        private readonly ConcurrentQueue<string> _queue = new();
        private readonly HashSet<string> _seen = new();
        private readonly LinkedList<string> _seenOrder = new();
        private readonly object _lock = new();

        public int Count => _queue.Count;

        public void Enqueue(string file)
        {
            lock (_lock)
            {
                if (_seen.Contains(file))
                    return;

                _seen.Add(file);
                _seenOrder.AddLast(file);
                _queue.Enqueue(file);

                while (_seen.Count > MaxSeenEntries)
                {
                    var oldest = _seenOrder.First?.Value;
                    if (oldest == null) break;
                    _seenOrder.RemoveFirst();
                    _seen.Remove(oldest!);
                }
            }
        }

        public bool TryDequeue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? file)
        {
            return _queue.TryDequeue(out file);
        }

        public void Clear()
        {
            lock (_lock)
            {
                while (_queue.TryDequeue(out _)) { }
                _seen.Clear();
                _seenOrder.Clear();
            }
        }
    }
}
