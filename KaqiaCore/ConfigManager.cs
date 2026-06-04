using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KaqiaCore
{
    public class ToolState
    {
        public string Color { get; set; } = "#FFFF0000"; // Default Red
        public double Thickness { get; set; } = 3.0;
    }

    public class AppConfig
    {
        // Basic Settings
        public string HotkeyModifiers { get; set; } = "Control,Alt";
        public string HotkeyKey { get; set; } = "S";
        public bool AutoStart { get; set; } = false;
        public string DefaultSavePath { get; set; } = "";

        // Beautify Parameters
        public double Radius { get; set; } = 0;
        public double StrokeThickness { get; set; } = 0;
        public double Padding { get; set; } = 0;
        public string StrokeColor { get; set; } = "#80FFFFFF";
        public string CanvasColor { get; set; } = "Transparent";
        public bool ShadowEnabled { get; set; } = true;

        // Tool States
        public Dictionary<string, ToolState> Tools { get; set; } = new Dictionary<string, ToolState>
        {
            { "Rectangle", new ToolState() },
            { "Ellipse", new ToolState() },
            { "Arrow", new ToolState() },
            { "Pen", new ToolState() },
            { "Text", new ToolState { Thickness = 0 } }
        };
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
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null) return config;
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
