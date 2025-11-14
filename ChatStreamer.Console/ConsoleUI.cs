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
    private readonly string _apiKey;

    public ConsoleUI(ChatService chatService, OpenAISettings openAiSettings, string apiKey)
    {
        // set console encoding to UTF-8
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        _chatService = chatService;
        _openAiSettings = openAiSettings;
        _apiKey = apiKey;
    }

    public async Task StartAsync()
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[You] ");
            Console.ResetColor();
            var userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput)) continue;

            if (userInput.StartsWith("/"))
            {
                await HandleCommand(userInput);
                continue;
            }

            await HandleUserInput(userInput);
        }
    }

    private async Task HandleUserInput(string userInput)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[Assistant] ");
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
        var frames = new[]
        {
            "⡿", "⣟", "⣯", "⣷", "⣾", "⣽", "⣻", "⢿"
        };

        var colors = new[]
        {
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Blue,
            ConsoleColor.Magenta,
            ConsoleColor.White,
            ConsoleColor.DarkYellow
        };

        int index = 0;

        ConsoleColor originalColor = Console.ForegroundColor;

        Console.CursorVisible = false;

        while (!token.IsCancellationRequested)
        {
            // Apply color for this frame
            Console.ForegroundColor = colors[index % colors.Length];

            // Write spinner character + one space (EXACTLY two characters)
            Console.Write(frames[index] + " ");

            // Wait for the next frame
            await Task.Delay(100);

            // Erase the spinner + space (two backspaces)
            Console.Write("\b\b");

            index = (index + 1) % frames.Length;
        }

        // Clean up any leftover spinner
        Console.Write("  ");   // overwrite two chars
        Console.Write("\b\b"); // move cursor back

        Console.ForegroundColor = originalColor;
    }
    private async Task HandleCommand(string userInput)
    {
        var parts = userInput.Trim().ToLower().Split(' ', 2);
        var command = parts[0];
        var args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (command)
        {
            case "/help":
                ShowInteractiveHelp();
                break;
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
                Console.WriteLine($"Model set to: {args}.");
                break;
            case "/listmodels":
                Console.WriteLine("Fetching available models...");
                try
                {
                    var availableModels = await ChatService.GetAvailableModelsAsync(_apiKey);
                    Console.WriteLine("Available models:");
                    foreach (var m in availableModels)
                    {
                        Console.WriteLine($"- {m}");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }
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

    private void ShowInteractiveHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  /help\t\t\tShow this help message.");
        Console.WriteLine("  /clear\t\tClear the current conversation.");
        Console.WriteLine("  /save <filename>\tSave the conversation to a file.");
        Console.WriteLine("  /load <filename>\tLoad a conversation from a file.");
        Console.WriteLine("  /model <model_name>\tSwitch to a different chat model.");
        Console.WriteLine("  /listmodels\t\tList all available models.");
        Console.WriteLine("  /exit\t\t\tExit the application.");
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
            Console.WriteLine($"Conversation saved to {fileName}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving conversation: {ex.Message}.");
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
            var serializableMessages = JsonSerializer.Deserialize<List<SerializableChatMessage>>(json);
            if (serializableMessages != null)
            {
                var messages = serializableMessages.Select(m => m.ToChatMessage()).ToList();
                _chatService.LoadMessages(messages);
                Console.WriteLine($"Conversation loaded from {fileName}.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading conversation: {ex.Message}.");
        }
    }
}
