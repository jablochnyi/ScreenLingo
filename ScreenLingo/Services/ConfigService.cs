using System.IO;
using System.Text.Json;
using ScreenLingo.Models;

namespace ScreenLingo.Services
{
    public static class ConfigService
    {
        private static readonly string configPath = "config.json";

        public static Settings Load()
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            return new Settings();
        }

        public static void Save(Settings settings)
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
    }
}
