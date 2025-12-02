using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

/// <summary>
/// Main entry point for the ChatStreamer console application.
/// Handles configuration loading, command-line argument parsing, and application initialization.
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point. Initializes configuration, validates settings, and starts the console UI.
    /// </summary>
    /// <param name="args">Command-line arguments. Supports /help, /listmodels, /model:, /systemprompt:, /load:, and initial prompt text.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Configuration hierarchy (highest to lowest precedence):
    /// 1. Command-line arguments (/model:, /systemprompt:)
    /// 2. Environment variables (OPENAI_API_KEY)
    /// 3. appsettings.json
    /// 4. User input (API key prompt if not found)
    /// </remarks>
    public static async Task Main(string[] args)
    {
        // Handle /help command early to avoid unnecessary initialization
        if (args.Contains("/help"))
        {
            ProgramHelpers.ShowHelp();
            return;
        }

        // Ensure clean console state on Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.CursorVisible = true;
            Console.ResetColor();
        };

        // Build configuration from appsettings.json and environment variables
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var openAiSettings = config.GetSection("OpenAI").Get<OpenAISettings>() ?? new OpenAISettings();

        // Resolve API key from environment variable, config file, or user input
        // Priority: Environment variable > Config file > User input
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            apiKey = openAiSettings.ApiKey;
        }
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.Write("Enter your OpenAI API key: ");
            apiKey = Console.ReadLine();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("API key is required.");
                return;
            }
        }

        // Check for /listmodels command
        if (args.Any(a => a.Equals("/listmodels", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Fetching available models...");
            var availableModels = await ChatService.GetAvailableModelsAsync(apiKey);
            Console.WriteLine("Available models:");
            foreach (var m in availableModels)
            {
                Console.WriteLine($"- {m}");
            }
            return;
        }

        // Load default settings from configuration
        var model = openAiSettings.Model;
        var systemPrompt = openAiSettings.SystemPrompt;
        string? loadFile = null;

        // Parse command-line arguments to override configuration
        foreach (var arg in args)
        {
            if (arg.StartsWith("/model:"))
            {
                model = arg.Substring("/model:".Length);
            }
            else if (arg.StartsWith("/systemprompt:"))
            {
                systemPrompt = arg.Substring("/systemprompt:".Length);
            }
            else if (arg.StartsWith("/load:"))
            {
                loadFile = arg.Substring("/load:".Length);
            }
        }

        // Validate the model against available models from OpenAI API
        try
        {
            var validModels = await ChatService.GetAvailableModelsAsync(apiKey);
            if (!validModels.Any() || !validModels.Contains(model))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Model '{model}' is not valid or no models could be fetched.");
                if (validModels.Any())
                {
                    Console.WriteLine($"Available models are: {string.Join(", ", validModels)}");
                }
                Console.ResetColor();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=================================================");
        Console.WriteLine("          Welcome to ChatStreamer");
        Console.WriteLine("=================================================");
        Console.ResetColor();
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"System Prompt: {systemPrompt}");
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine("Type '/help' to see available commands.");
        Console.WriteLine("Type '/exit' to quit the application.");
        Console.WriteLine("-------------------------------------------------");

        // Initialize chat service with validated configuration
        var chatService = new ChatService(apiKey, model!, systemPrompt ?? "You are a helpful assistant.");
        
        // Extract initial prompt from non-command arguments
        var initialPromptArgs = args.Where(a => !a.StartsWith("/")).ToArray();
        var initialPrompt = initialPromptArgs.Length > 0 ? string.Join(' ', initialPromptArgs) : null;
        
        // Create console UI with or without initial prompt
        var consoleUI = initialPrompt != null 
            ? new ConsoleUI(chatService, openAiSettings, apiKey, initialPrompt)
            : new ConsoleUI(chatService, openAiSettings, apiKey);

        // Load conversation from file if specified
        if (!string.IsNullOrEmpty(loadFile))
        {
            consoleUI.LoadConversation(loadFile);
        }

        // Start the console UI and ensure clean exit
        try
        {
            await consoleUI.StartAsync();
        }
        finally
        {
            Console.CursorVisible = true;
            Console.ResetColor();
        }
    }
}

/// <summary>
/// Configuration model for OpenAI settings loaded from appsettings.json.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Gets or sets the OpenAI model to use (e.g., "gpt-4", "gpt-3.5-turbo").
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Gets or sets the system prompt that defines the AI's behavior and personality.
    /// </summary>
    public string? SystemPrompt { get; set; }
    
    /// <summary>
    /// Gets or sets the OpenAI API key. Environment variable OPENAI_API_KEY takes precedence.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Helper methods for the Program class.
/// </summary>
public static class ProgramHelpers
{
    /// <summary>
    /// Displays command-line help information including usage, options, and examples.
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine("Usage: ChatStreamerApp [options] [initial prompt]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  /help\t\t\t\tShow this help message.");
        Console.WriteLine("  /model:<model_name>\t\tSet the OpenAI model to use.");
        Console.WriteLine("  /systemprompt:<prompt>\tSet the system prompt.");
        Console.WriteLine("  /load:<file_name>\t\tLoad a conversation from a file.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ChatStreamerApp /model:gpt-3.5-turbo \"Hello, world!\"");
        Console.WriteLine("  ChatStreamerApp /load:my_conversation.json");
    }
}