using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChatBot.Models;
using Newtonsoft.Json;

namespace ChatBot.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;

        public AiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> SendMessageAsync(ConnectionConfig connection, string systemPrompt, List<ChatMessage> messages)
        {
            switch (connection.Provider)
            {
                case ProviderType.OpenRouter:
                    return await SendOpenRouterAsync(connection, systemPrompt, messages);
                case ProviderType.LLM:
                    return await SendLLMAsync(connection, systemPrompt, messages);
                default:
                    throw new NotSupportedException($"Provider {connection.Provider} is not supported.");
            }
        }

        private async Task<string> SendOpenRouterAsync(ConnectionConfig connection, string systemPrompt, List<ChatMessage> messages)
        {
            var url = string.IsNullOrEmpty(connection.BaseUrl)
                ? "https://openrouter.ai/api/v1/chat/completions"
                : connection.BaseUrl;

            var apiMessages = new List<object>();
            apiMessages.Add(new { role = "system", content = systemPrompt });
            foreach (var msg in messages)
            {
                apiMessages.Add(new { role = msg.Role.ToLower(), content = msg.Content });
            }

            var payload = new
            {
                model = connection.Model,
                messages = apiMessages
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {connection.ApiKey}");
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://localhost");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "ChatBot");

            var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: {response.StatusCode}\n{responseJson}";
            }

            var result = JsonConvert.DeserializeObject<OpenRouterResponse>(responseJson);
            return result?.Choices?[0]?.Message?.Content ?? "No response";
        }

        private async Task<string> SendLLMAsync(ConnectionConfig connection, string systemPrompt, List<ChatMessage> messages)
        {
            if (string.IsNullOrEmpty(connection.BaseUrl))
                throw new InvalidOperationException("Base URL is required for LLM provider");

            var url = connection.BaseUrl;

            if (url.Contains("/api/v1/generate"))
            {
                return await SendKoboldCppLegacyAsync(connection, systemPrompt, messages);
            }

            var apiMessages = new List<object>();
            apiMessages.Add(new { role = "system", content = systemPrompt });
            foreach (var msg in messages)
            {
                apiMessages.Add(new { role = msg.Role.ToLower(), content = msg.Content });
            }

            var payload = new Dictionary<string, object>
            {
                { "messages", apiMessages }
            };

            if (!string.IsNullOrEmpty(connection.Model))
                payload["model"] = connection.Model;

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(connection.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {connection.ApiKey}");
            }

            var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: {response.StatusCode}\n{responseJson}";
            }

            try
            {
                var openaiResult = JsonConvert.DeserializeObject<OpenRouterResponse>(responseJson);
                if (!string.IsNullOrEmpty(openaiResult?.Choices?[0]?.Message?.Content))
                    return openaiResult.Choices[0].Message.Content;

                var koboldResult = JsonConvert.DeserializeObject<KoboldCppResponse>(responseJson);
                if (!string.IsNullOrEmpty(koboldResult?.Choices?[0]?.Text))
                    return koboldResult.Choices[0].Text;

                return $"Неизвестный формат ответа:\n{responseJson}";
            }
            catch (Exception ex)
            {
                return $"Ошибка парсинга ответа: {ex.Message}\n{responseJson}";
            }
        }

        private async Task<string> SendKoboldCppLegacyAsync(ConnectionConfig connection, string systemPrompt, List<ChatMessage> messages)
        {
            var prompt = systemPrompt + "\n\n";
            foreach (var msg in messages)
            {
                var role = msg.Role.ToLower() == "user" ? "User" : "Assistant";
                prompt += $"{role}: {msg.Content}\n\n";
            }
            prompt += "Assistant:";

            var payload = new
            {
                prompt = prompt,
                max_context_length = 4096,
                max_length = 512
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();

            var response = await _httpClient.PostAsync(connection.BaseUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: {response.StatusCode}\n{responseJson}";
            }

            try
            {
                var result = JsonConvert.DeserializeObject<KoboldCppLegacyResponse>(responseJson);
                if (result?.Results?.Count > 0 && !string.IsNullOrEmpty(result.Results[0].Text))
                    return result.Results[0].Text.TrimStart();

                return $"Неизвестный формат ответа:\n{responseJson}";
            }
            catch (Exception ex)
            {
                return $"Ошибка парсинга ответа: {ex.Message}\n{responseJson}";
            }
        }
    }

    public class OpenRouterResponse
    {
        public List<Choice> Choices { get; set; } = new();
    }

    public class Choice
    {
        public MessageData Message { get; set; } = new();
        public string Text { get; set; } = "";
    }

    public class MessageData
    {
        public string Content { get; set; } = "";
    }

    public class KoboldCppResponse
    {
        public List<KoboldChoice> Choices { get; set; } = new();
    }

    public class KoboldChoice
    {
        public string Text { get; set; } = "";
    }

    public class KoboldCppLegacyResponse
    {
        public List<KoboldLegacyResult> Results { get; set; } = new();
    }

    public class KoboldLegacyResult
    {
        public string Text { get; set; } = "";
    }
}
