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

/// <summary>
/// Provides a wrapper service for interacting with the OpenAI Chat API.
/// Manages conversation history, streaming responses, and model configuration.
/// </summary>
public class ChatService
{
    // Static cache for available models (shared across all instances)
    private static List<string>? _availableModels;
    private static DateTime _availableModelsLastCacheTime;

    private ChatClient _client;
    private readonly List<ChatMessage> _messages;
    private readonly string _model;

    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the ChatService class.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key for authentication.</param>
    /// <param name="model">The OpenAI model to use (e.g., "gpt-4", "gpt-3.5-turbo").</param>
    /// <param name="systemPrompt">The system prompt that defines the AI's behavior.</param>
    public ChatService(string apiKey, string model, string systemPrompt)
    {
        _apiKey = apiKey;
        _client = new ChatClient(model, _apiKey);
        _model = model;
        _messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };
    }

    /// <summary>
    /// Gets the read-only list of conversation messages.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Sends a user message to the OpenAI API and streams the response back token by token.
    /// </summary>
    /// <param name="userInput">The user's message.</param>
    /// <returns>An async enumerable of response tokens as they arrive from the API.</returns>
    /// <remarks>
    /// Adds the user message to conversation history, streams the response, and adds the complete
    /// assistant response to history. On error, removes the user message and yields an error message.
    /// Handles authentication errors, rate limits, network errors, and general API errors.
    /// </remarks>
    public async IAsyncEnumerable<string> GetChatResponseStreamingAsync(string userInput)
    {
        _messages.Add(ChatMessage.CreateUserMessage(userInput));
        var assistantResponse = new List<string>();
        var responseStream = _client.CompleteChatStreamingAsync(_messages);
        await using var enumerator = responseStream.GetAsyncEnumerator();
        string? errorMessage = null;

        // Stream response tokens until complete or error occurs
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex)
            {
                // Remove user message on error to keep conversation history clean
                _messages.RemoveAt(_messages.Count - 1);
                // Categorize error for user-friendly message
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

            // Yield each content token as it arrives
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

        // Add complete assistant response to conversation history
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

    /// <summary>
    /// Clears all conversation messages except the system prompt.
    /// </summary>
    public void ClearMessages()
    {
        var systemMessage = _messages[0];
        _messages.Clear();
        _messages.Add(systemMessage);
    }

    /// <summary>
    /// Loads a list of messages into the conversation history, replacing existing messages.
    /// </summary>
    /// <param name="messages">The list of messages to load.</param>
    public void LoadMessages(List<ChatMessage> messages)
    {
        _messages.Clear();
        _messages.AddRange(messages);
    }

    /// <summary>
    /// Changes the OpenAI model used for chat completions.
    /// </summary>
    /// <param name="model">The new model name (e.g., "gpt-4", "gpt-3.5-turbo").</param>
    public void SetModel(string model)
    {
        _client = new ChatClient(model, _apiKey);
    }

    /// <summary>
    /// Updates the system prompt that defines the AI's behavior.
    /// </summary>
    /// <param name="newPrompt">The new system prompt text.</param>
    /// <remarks>
    /// Replaces the first message in the conversation history (which should be the system message).
    /// </remarks>
    public void SetSystemPrompt(string newPrompt)
    {
        if (_messages.Count > 0 && _messages[0].Content.Count > 0)
        {
            _messages[0] = ChatMessage.CreateSystemMessage(newPrompt);
        }
        else
        {
             // Should not happen given constructor, but safe fallback
            _messages.Insert(0, ChatMessage.CreateSystemMessage(newPrompt));
        }
    }

    /// <summary>
    /// Retrieves the current system prompt.
    /// </summary>
    /// <returns>The system prompt text, or an empty string if not found.</returns>
    public string GetSystemPrompt()
    {
        if (_messages.Count > 0 && _messages[0].Content.Count > 0)
        {
            return _messages[0].Content[0].Text;
        }
        return string.Empty;
    }

    /// <summary>
    /// Fetches the list of available OpenAI models from the API.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key for authentication.</param>
    /// <returns>A read-only list of available GPT model names.</returns>
    /// <remarks>
    /// Results are cached for 1 hour to reduce API calls.
    /// Only returns models with IDs starting with "gpt" (chat completion models).
    /// </remarks>
    /// <exception cref="Exception">Thrown when the API request fails or response cannot be parsed.</exception>
    public static async Task<IReadOnlyList<string>> GetAvailableModelsAsync(string apiKey)
    {
        // Check cache (1-hour TTL)
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

                // Filter for GPT models only (chat completion models)
                _availableModels = modelListResponse?.Data
                    .Where(model => model.Id.StartsWith("gpt"))
                    .Select(model => model.Id)
                    .ToList() ?? new List<string>();

                _availableModelsLastCacheTime = DateTime.Now;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Failed to fetch available models due to a network error. Please check your connection and API key in app settings or OPENAI_API_KEY environment variable.", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception("Failed to parse the list of available models from the API.", ex);
            }
        }
        return _availableModels.AsReadOnly();
    }
}

/// <summary>
/// Represents the response from the OpenAI models API endpoint.
/// </summary>
public class ModelListResponse
{
    /// <summary>
    /// Gets or sets the list of available models.
    /// </summary>
    [JsonPropertyName("data")]
    public List<Model> Data { get; set; } = new List<Model>();
}

/// <summary>
/// Represents a single OpenAI model.
/// </summary>
public class Model
{
    /// <summary>
    /// Gets or sets the model ID (e.g., "gpt-4", "gpt-3.5-turbo").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
