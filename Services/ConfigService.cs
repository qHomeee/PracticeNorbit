using System.IO;
using System.Text.Json;
using AutoPrint.Models;

namespace AutoPrint.Services
{
    public class ConfigService
    {
        private const string FileName = "Config.json";

        public AppConfig Load()
        {
            if (!File.Exists(FileName))
                return new AppConfig();

            return JsonSerializer.Deserialize<AppConfig>(
                File.ReadAllText(FileName)
            );
        }

        public void Save(AppConfig config)
        {
            File.WriteAllText(FileName,
                JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
    }
}