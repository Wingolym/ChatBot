using System.Collections.Generic;

namespace ChatBot.Models
{
    public class ChatSession
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Title { get; set; } = "Новый чат";
        public List<ChatMessage> Messages { get; set; } = new();
        public long CreatedAt { get; set; } = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long UpdatedAt { get; set; } = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}