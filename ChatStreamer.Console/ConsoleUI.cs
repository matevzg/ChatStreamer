using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

#nullable enable

/// <summary>
/// Manages the console-based user interface for the ChatStreamer application.
/// Handles user input, displays AI responses with streaming, manages typing indicators, and processes commands.
/// </summary>
public class ConsoleUI
{
    private readonly ChatService _chatService;
    private readonly OpenAISettings _openAiSettings;
    private readonly string _apiKey;
    private readonly string? _initialPrompt;

    /// <summary>
    /// Initializes a new instance of the ConsoleUI class.
    /// </summary>
    /// <param name="chatService">The chat service for handling AI interactions.</param>
    /// <param name="openAiSettings">OpenAI configuration settings.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    public ConsoleUI(ChatService chatService, OpenAISettings openAiSettings, string apiKey)
    {
        // Set console encoding to UTF-8 for proper character display
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        _chatService = chatService;
        _openAiSettings = openAiSettings;
        _apiKey = apiKey;
    }

    /// <summary>
    /// Initializes a new instance of the ConsoleUI class with an initial prompt.
    /// </summary>
    /// <param name="chatService">The chat service for handling AI interactions.</param>
    /// <param name="openAiSettings">OpenAI configuration settings.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="initialPrompt">An initial message to send automatically when the UI starts.</param>
    public ConsoleUI(ChatService chatService, OpenAISettings openAiSettings, string apiKey, string initialPrompt)
        : this(chatService, openAiSettings, apiKey)
    {
        _initialPrompt = initialPrompt;
    }

    /// <summary>
    /// Starts the main console UI loop. Handles user input, processes commands, and displays AI responses.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// On first iteration, sends the initial prompt if provided, or sends an automated greeting if no conversation history exists.
    /// Continues looping until the user exits via the /exit command.
    /// </remarks>
    public async Task StartAsync()
    {
        bool isFirstIteration = true;

        while (true)
        {
            string? userInput;

            // Handle initial prompt or automated message on first iteration
            if (isFirstIteration)
            {
                isFirstIteration = false;

                if (!string.IsNullOrEmpty(_initialPrompt))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"[You] {_initialPrompt}");
                    Console.ResetColor();
                    Console.WriteLine();
                    userInput = _initialPrompt;
                }
                // Check if we only have the system message (count == 1)
                else if (_chatService.Messages.Count == 1)
                {
                    await SendAutomatedMessage("Hi there, please introduce yourself.");
                    continue;
                }
                else
                {
                    // No initial prompt and messages exist, proceed to normal input
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("[You] ");
                    Console.ResetColor();
                    userInput = Console.ReadLine();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[You] ");
                Console.ResetColor();
                userInput = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(userInput)) continue;

            if (userInput.StartsWith("/"))
            {
                if (await HandleCommand(userInput)) return;
                continue;
            }

            var shouldExit = await ExecuteWithInputBlocking(() => HandleUserInput(userInput));
            if (shouldExit) break;
        }
    }

    /// <summary>
    /// Executes an asynchronous operation while blocking and discarding user keyboard input.
    /// </summary>
    /// <typeparam name="T">The return type of the async operation.</typeparam>
    /// <param name="asyncOperation">The async operation to execute.</param>
    /// <returns>The result of the async operation.</returns>
    /// <remarks>
    /// This prevents user input from appearing in the console while the AI is generating a response.
    /// A background task continuously clears the keyboard buffer until the operation completes.
    /// </remarks>
    private async Task<T> ExecuteWithInputBlocking<T>(Func<Task<T>> asyncOperation)
    {
        var cts = new CancellationTokenSource();
        var inputClearTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }
        }, cts.Token);

        try
        {
            return await asyncOperation();
        }
        finally
        {
            cts.Cancel();
            try { await inputClearTask; } catch { }
        }
    }

    /// <summary>
    /// Executes an asynchronous operation while blocking and discarding user keyboard input.
    /// Non-generic overload for operations that don't return a value.
    /// </summary>
    /// <param name="asyncOperation">The async operation to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteWithInputBlocking(Func<Task> asyncOperation)
    {
        await ExecuteWithInputBlocking(async () =>
        {
            await asyncOperation();
            return 0; // Dummy return value
        });
    }

    /// <summary>
    /// Sends an automated message (not from user input) and displays the AI's response.
    /// </summary>
    /// <param name="message">The automated message to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Used for initial greetings or automated prompts after system prompt changes.
    /// The message is labeled as "[You, automated]" in the console.
    /// </remarks>
    private async Task SendAutomatedMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"[You, automated] {message}");
        Console.ResetColor();
        Console.WriteLine();

        await ExecuteWithInputBlocking(() => HandleUserInput(message));
    }

    /// <summary>
    /// Processes user input and displays the streaming AI response.
    /// </summary>
    /// <param name="userInput">The user's message.</param>
    /// <returns>False (reserved for future use to indicate exit condition).</returns>
    /// <remarks>
    /// Shows a typing indicator while waiting for the first response token,
    /// then streams the response in real-time as tokens arrive from the API.
    /// </remarks>
    public async Task<bool> HandleUserInput(string userInput)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[Assistant] ");
        Console.ResetColor();

        // Start typing indicator animation while waiting for first response token
        var cancellationTokenSource = new CancellationTokenSource();
        var indicatorTask = ShowTypingIndicator(cancellationTokenSource.Token);

        var firstPart = true;
        await foreach (var part in _chatService.GetChatResponseStreamingAsync(userInput))
        {
            // Cancel typing indicator and wait for it to clean up before showing response
            if (firstPart)
            {
                cancellationTokenSource.Cancel();
                await indicatorTask;
                firstPart = false;
            }
            Console.Write(part);
        }
        Console.WriteLine();
        return false;
    }
    /// <summary>
    /// Displays an animated typing indicator (spinner) while waiting for the AI response.
    /// </summary>
    /// <param name="token">Cancellation token to stop the animation when the first response token arrives.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Uses a colorful spinning animation with Unicode characters.
    /// Hides the cursor during animation and cleans up properly when cancelled.
    /// </remarks>
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

        try
        {
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
        }
        finally
        {
            // Clean up any leftover spinner
            Console.Write("  ");   // overwrite two chars
            Console.Write("\b\b"); // move cursor back

            Console.ForegroundColor = originalColor;
            Console.CursorVisible = true;
        }
    }
    /// <summary>
    /// Processes slash commands entered by the user.
    /// </summary>
    /// <param name="userInput">The command string (must start with '/').</param>
    /// <returns>True if the application should exit, false otherwise.</returns>
    /// <remarks>
    /// Supported commands: /help, /clear, /save, /load, /model, /systemprompt, /listmodels, /exit
    /// </remarks>
    private async Task<bool> HandleCommand(string userInput)
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
            case "/systemprompt":
                if (string.IsNullOrWhiteSpace(args))
                {
                    var currentPrompt = _chatService.GetSystemPrompt();
                    Console.WriteLine($"Current System Prompt: {currentPrompt}");
                }
                else
                {
                    _chatService.SetSystemPrompt(args);
                    Console.WriteLine("System prompt updated.");
                    await SendAutomatedMessage("Hi there, please introduce yourself.");
                }
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
                string exitMessage = "We are done for this session, see you soon. Thank you.";
                
                await SendAutomatedMessage(exitMessage);
                return true;
            default:
                Console.WriteLine($"Unknown command: {command}");
                break;
        }
        return false;
    }

    /// <summary>
    /// Displays the list of available interactive commands.
    /// </summary>
    private void ShowInteractiveHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  /help\t\t\t\tShow this help message.");
        Console.WriteLine("  /clear\t\t\tClear the current conversation.");
        Console.WriteLine("  /save <filename>\t\tSave the conversation to a file.");
        Console.WriteLine("  /load <filename>\t\tLoad a conversation from a file.");
        Console.WriteLine("  /model <model_name>\t\tSwitch to a different chat model.");
        Console.WriteLine("  /systemprompt <prompt>\tDisplay or update the system prompt.");
        Console.WriteLine("  /listmodels\t\t\tList all available models.");
        Console.WriteLine("  /exit\t\t\t\tExit the application.");
    }

    /// <summary>
    /// Saves the current conversation to a JSON file.
    /// </summary>
    /// <param name="fileName">The name of the file to save to.</param>
    /// <remarks>
    /// Serializes all messages (including system, user, and assistant messages) to JSON format.
    /// </remarks>
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

    /// <summary>
    /// Loads a conversation from a JSON file and restores the chat history.
    /// </summary>
    /// <param name="fileName">The name of the file to load from.</param>
    /// <remarks>
    /// Deserializes messages from JSON and replaces the current conversation history.
    /// </remarks>
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
