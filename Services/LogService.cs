using System;
using System.IO;

namespace AutoPrint.Services
{
    public class LogService
    {
        private readonly string _file = "Logs/app.log";
        private readonly object _lock = new();

        public void Info(string msg) => Write("INFO", msg);
        public void Error(string msg) => Write("ERROR", msg);

        private void Write(string level, string msg)
        {
            lock (_lock)
            {
                Directory.CreateDirectory("Logs");

                File.AppendAllText(_file,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
            }
        }
    }
}