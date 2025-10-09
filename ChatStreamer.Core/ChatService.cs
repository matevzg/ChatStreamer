using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI;
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
        await using var enumerator = responseStream.GetAsyncEnumerator();
        string? errorMessage = null;

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                _messages.RemoveAt(_messages.Count - 1); // Remove the user message that caused the error
                if (ex.Message.Contains("authentication"))
                {
                    errorMessage = "Invalid API key. Please check your configuration.";
                }
                else if (ex.Message.Contains("rate limit"))
                {
                    errorMessage = "API rate limit exceeded. Please try again later.";
                }
                else if (ex is HttpRequestException)
                {
                    errorMessage = $"A network error occurred: {ex.Message}";
                }
                else
                {
                    errorMessage = $"An API error occurred: {ex.Message}";
                }
                hasNext = false; // Stop the loop on error
            }

            if (errorMessage != null)
            {
                yield return $"[ERROR: {errorMessage}]";
                break;
            }

            if (!hasNext)
            {
                break;
            }

            var update = enumerator.Current;
            if (update.ContentUpdate.Count > 0)
            {
                foreach (var contentPart in update.ContentUpdate)
                {
                    yield return contentPart.Text;
                    assistantResponse.Add(contentPart.Text);
                }
            }
        }

        if (errorMessage == null)
        {
            var fullResponse = string.Join("", assistantResponse);
            if (assistantResponse.Any())
            {
                _messages.Add(ChatMessage.CreateAssistantMessage(fullResponse));
            }
            else
            {
                // If the response was empty, remove the user message to prevent clutter.
                _messages.RemoveAt(_messages.Count - 1);
            }
        }
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
            try
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
            catch (HttpRequestException ex)
            {
                throw new Exception("Failed to fetch available models due to a network error. Please check your connection and API key.", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception("Failed to parse the list of available models from the API.", ex);
            }
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
