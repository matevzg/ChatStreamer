using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

#nullable enable

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("/help"))
        {
            ProgramHelpers.ShowHelp();
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var openAiSettings = config.GetSection("OpenAI").Get<OpenAISettings>() ?? new OpenAISettings();

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

        var model = openAiSettings.Model ?? "gpt-4o";
        var systemPrompt = openAiSettings.SystemPrompt ?? "You are a helpful assistant.";
        string? loadFile = null;

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

        // Validate the model
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

        Console.WriteLine($"> Model: {model}");
        Console.WriteLine($"> System Prompt: {systemPrompt}");
        Console.WriteLine("> Type '/help' for a list of commands or '/exit' to quit.");

        var chatService = new ChatService(apiKey, model, systemPrompt);
        var consoleUI = new ConsoleUI(chatService, openAiSettings, apiKey);

        if (!string.IsNullOrEmpty(loadFile))
        {
            consoleUI.LoadConversation(loadFile);
        }

        var initialPromptArgs = args.Where(a => !a.StartsWith("/")).ToArray();
        if (initialPromptArgs.Length > 0)
        {
            var initialPrompt = string.Join(' ', initialPromptArgs);
            Console.WriteLine($"[user] {initialPrompt}");
            Console.Write("[assistant] ");
            await foreach (var part in chatService.GetChatResponseStreamingAsync(initialPrompt))
            {
                Console.Write(part);
            }
            Console.WriteLine();
        }

        await consoleUI.StartAsync();
    }
}

public class OpenAISettings
{
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    public string? ApiKey { get; set; }
}

public static class ProgramHelpers
{
    public static void ShowHelp()
    {
        Console.WriteLine("Usage: ChatStreamerApp [options] [initial prompt]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  /help\t\t\tShow this help message.");
        Console.WriteLine("  /model:<model_name>\t\tSet the OpenAI model to use.");
        Console.WriteLine("  /systemprompt:<prompt>\tSet the system prompt.");
        Console.WriteLine("  /load:<file_name>\t\tLoad a conversation from a file.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ChatStreamerApp /model:gpt-3.5-turbo \"Hello, world!\"");
        Console.WriteLine("  ChatStreamerApp /load:my_conversation.json");
    }
}