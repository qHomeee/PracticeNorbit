using System;
using System.IO;
using System.Text.Json;
using AutoPrint.Models;

namespace AutoPrint.Services
{
    public class ConfigService
    {
        private const string FileName = "Config.json";
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public AppConfig Load()
        {
            try
            {
                if (!File.Exists(FileName))
                    return new AppConfig();

                string content = File.ReadAllText(FileName).Trim();
                if (string.IsNullOrEmpty(content))
                    return new AppConfig();

                return JsonSerializer.Deserialize<AppConfig>(content, _jsonOptions) ?? new AppConfig();
            }
            catch (Exception)
            {
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                string tempFile = FileName + ".tmp";
                File.WriteAllText(tempFile, json);
                if (File.Exists(FileName))
                    File.Replace(tempFile, FileName, null);
                else
                    File.Move(tempFile, FileName);
            }
            catch (Exception)
            {
            }
        }
    }
}
