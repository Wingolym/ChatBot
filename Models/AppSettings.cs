using System.Collections.Generic;

namespace ChatBot.Models
{
    public class AppSettings
    {
        public string DefaultConnectionId { get; set; } = "";
        public List<ConnectionConfig> Connections { get; set; } = new();
        public string SystemPrompt { get; set; } = "Ты полезный AI-ассистент. Отвечай на вопросы пользователя кратко и по делу.";
    }
}
