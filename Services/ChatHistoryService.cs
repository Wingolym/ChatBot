using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChatBot.Models;
using Newtonsoft.Json;

namespace ChatBot.Services
{
    public class ChatHistoryService
    {
        private readonly string _historyPath;
        private List<ChatSession> _sessions;

        public List<ChatSession> Sessions => _sessions;

        public ChatHistoryService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appData, "ChatBot");
            Directory.CreateDirectory(appDir);
            _historyPath = Path.Combine(appDir, "chat_history.json");

            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                _sessions = JsonConvert.DeserializeObject<List<ChatSession>>(json) ?? new List<ChatSession>();
            }
            else
            {
                _sessions = new List<ChatSession>();
            }

            _sessions = _sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(_sessions, Formatting.Indented);
            File.WriteAllText(_historyPath, json);
        }

        public void AddSession(ChatSession session)
        {
            _sessions.Insert(0, session);
            Save();
        }

        public void UpdateSession(ChatSession session)
        {
            var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Messages = session.Messages;
                existing.Title = session.Title;
                existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            Save();
        }

        public void DeleteSession(string id)
        {
            _sessions.RemoveAll(s => s.Id == id);
            Save();
        }

        public ChatSession? GetSession(string id)
        {
            return _sessions.FirstOrDefault(s => s.Id == id);
        }
    }
}