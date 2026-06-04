using System;
using System.IO;
using System.Text.Json;

namespace KaqiaCore
{
    public class AppConfig
    {
        public string HotkeyModifiers { get; set; } = "Control,Alt";
        public string HotkeyKey { get; set; } = "S";
        public bool AutoStart { get; set; } = false;
        public string DefaultSavePath { get; set; } = "";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kaqia", "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string? dir = Path.GetDirectoryName(ConfigPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
