using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.Chat;

#nullable enable

public class ChatService
{
    private static List<string>? _availableModels;
    private static DateTime _availableModelsLastCacheTime;

    private ChatClient _client;
    private readonly List<ChatMessage> _messages;
    private readonly string _model;

    private readonly string _apiKey;

    public ChatService(string apiKey, string model, string systemPrompt)
    {
        _apiKey = apiKey;
        _client = new ChatClient(model, _apiKey);
        _model = model;
        _messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };
    }

    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    public async IAsyncEnumerable<string> GetChatResponseStreamingAsync(string userInput)
    {
        _messages.Add(ChatMessage.CreateUserMessage(userInput));
        var assistantResponse = new List<string>();
        var responseStream = _client.CompleteChatStreamingAsync(_messages);

        await foreach (var update in responseStream)
        {
            if (update.ContentUpdate.Count > 0)
            {
                foreach (var contentPart in update.ContentUpdate)
                {
                    yield return contentPart.Text;
                    assistantResponse.Add(contentPart.Text);
                }
            }
        }

        var fullResponse = string.Join("", assistantResponse);
        _messages.Add(ChatMessage.CreateAssistantMessage(fullResponse));
    }

    public void ClearMessages()
    {
        var systemMessage = _messages[0];
        _messages.Clear();
        _messages.Add(systemMessage);
    }

    public void LoadMessages(List<ChatMessage> messages)
    {
        _messages.Clear();
        _messages.AddRange(messages);
    }

    public void SetModel(string model)
    {
        _client = new ChatClient(model, _apiKey);
    }

    public static async Task<IReadOnlyList<string>> GetAvailableModelsAsync(string apiKey)
    {
        if (_availableModels == null || (DateTime.Now - _availableModelsLastCacheTime).TotalHours > 1)
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            HttpResponseMessage response = await httpClient.GetAsync("https://api.openai.com/v1/models");
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var modelListResponse = JsonSerializer.Deserialize<ModelListResponse>(jsonResponse);

            _availableModels = modelListResponse?.Data
                .Where(model => model.Id.StartsWith("gpt")) // Filter for chat completion models
                .Select(model => model.Id)
                .ToList() ?? new List<string>();

            _availableModelsLastCacheTime = DateTime.Now;
        }
        return _availableModels.AsReadOnly();
    }
}

public class ModelListResponse
{
    [JsonPropertyName("data")]
    public List<Model> Data { get; set; } = new List<Model>();
}

public class Model
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
