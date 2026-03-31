namespace ChatBot.Models
{
    public enum ProviderType
    {
        OpenRouter,
        LLM
    }

    public class ConnectionConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public ProviderType Provider { get; set; } = ProviderType.OpenRouter;
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
        public string BaseUrl { get; set; } = "";
    }
}
