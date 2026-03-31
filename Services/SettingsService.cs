using System;
using System.IO;
using ChatBot.Models;
using Newtonsoft.Json;

namespace ChatBot.Services
{
    public class SettingsService
    {
        private readonly string _configPath;

        public AppSettings Settings { get; private set; }

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appData, "ChatBot");
            Directory.CreateDirectory(appDir);
            _configPath = Path.Combine(appDir, "settings.json");

            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
                Save();
            }
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        public ConnectionConfig? GetActiveConnection()
        {
            if (string.IsNullOrEmpty(Settings.DefaultConnectionId))
                return null;
            return Settings.Connections.Find(c => c.Id == Settings.DefaultConnectionId);
        }
    }
}
