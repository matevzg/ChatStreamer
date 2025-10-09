using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

#nullable enable

public class ConsoleUI
{
    private readonly ChatService _chatService;
    private readonly OpenAISettings _openAiSettings;

    public ConsoleUI(ChatService chatService, OpenAISettings openAiSettings)
    {
        _chatService = chatService;
        _openAiSettings = openAiSettings;
    }

    public async Task StartAsync()
    {
        while (true)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[user] ");
            Console.ResetColor();
            var userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput)) continue;

            if (userInput.StartsWith("/"))
            {
                HandleCommand(userInput);
                continue;
            }

            await HandleUserInput(userInput);
        }
    }

    private async Task HandleUserInput(string userInput)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[assistant] ");
        Console.ResetColor();

        var cancellationTokenSource = new CancellationTokenSource();
        var indicatorTask = ShowTypingIndicator(cancellationTokenSource.Token);

        var firstPart = true;
        await foreach (var part in _chatService.GetChatResponseStreamingAsync(userInput))
        {
            if (firstPart)
            {
                cancellationTokenSource.Cancel();
                await indicatorTask;
                firstPart = false;
            }
            Console.Write(part);
        }
        Console.WriteLine();
    }

    private async Task ShowTypingIndicator(CancellationToken token)
    {
        var animation = new[] { "|", "/", "-", "\\" };
        var animationIndex = 0;
        while (!token.IsCancellationRequested)
        {
            Console.Write(animation[animationIndex]);
            await Task.Delay(100);
            Console.Write("\b");
            animationIndex = (animationIndex + 1) % animation.Length;
        }
    }

    private void HandleCommand(string userInput)
    {
        var parts = userInput.Trim().ToLower().Split(' ', 2);
        var command = parts[0];
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (command)
        {
            case "/clear":
                _chatService.ClearMessages();
                Console.WriteLine("Conversation cleared.");
                break;
            case "/save":
                SaveConversation(args);
                break;
            case "/load":
                LoadConversation(args);
                break;
            case "/model":
                _chatService.SetModel(args);
                Console.WriteLine($"Model set to: {args}");
                break;
            case "/exit":
                Console.WriteLine("Goodbye!");
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                break;
        }
    }

    private void SaveConversation(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Console.WriteLine("Please provide a file name.");
            return;
        }

        try
        {
            var serializableMessages = _chatService.Messages.Select(m => new SerializableChatMessage(m)).ToList();
            var json = JsonSerializer.Serialize(serializableMessages, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fileName, json);
            Console.WriteLine($"Conversation saved to {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving conversation: {ex.Message}");
        }
    }

    public void LoadConversation(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Console.WriteLine("Please provide a file name.");
            return;
        }

        if (!File.Exists(fileName))
        {
            Console.WriteLine($"File not found: {fileName}");
            return;
        }

        try
        {
            var json = File.ReadAllText(fileName);
            var mg1Messages = JsonSerializer.Deserialize<List<Mg1ChatMessage>>(json);
            if (mg1Messages != null)
            {
                var messages = new List<ChatMessage>();
                if (mg1Messages.Count > 0)
                {
                    messages.Add(ChatMessage.CreateSystemMessage(mg1Messages[0].Content[0].Text));
                }
                for (int i = 1; i < mg1Messages.Count; i++)
                {
                    if (i % 2 != 0) // Odd index, User
                    {
                        messages.Add(ChatMessage.CreateUserMessage(mg1Messages[i].Content[0].Text));
                    }
                    else // Even index, Assistant
                    {
                        messages.Add(ChatMessage.CreateAssistantMessage(mg1Messages[i].Content[0].Text));
                    }
                }

                _chatService.LoadMessages(messages);
                Console.WriteLine($"Conversation loaded from {fileName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading conversation: {ex.Message}");
        }
    }
}

public class Mg1ChatMessage
{
    public required List<Mg1ContentItem> Content { get; set; }
}

public class Mg1ContentItem
{
    public required string Text { get; set; }
}
