# ChatStreamer Solution Documentation

This document provides a comprehensive overview of the ChatStreamer solution, including its architecture, components, and usage.

## 1. Project Overview

ChatStreamer is a .NET 9 console application that enables interactive chat with the OpenAI API. It leverages the official OpenAI .NET SDK to provide a seamless chat experience with features like streaming responses, conversation history management, and model selection.

The solution is designed with a clear separation of concerns, making it easy to understand, maintain, and extend. The main components are:

*   **`Program.cs`**: The entry point of the application, responsible for parsing command-line arguments, loading configuration, and initializing the chat service.
*   **`ChatService.cs`**: The core component that handles all interactions with the OpenAI API, including sending chat requests, managing conversation history, and streaming responses.
*   **`ConsoleUI.cs`**: The user interface of the application, responsible for handling user input, displaying chat messages, and processing commands.
*   **`SerializableChatMessage.cs`**: A helper class that facilitates the serialization and deserialization of chat messages, enabling the saving and loading of conversations.

## 2. Command-Line Usage

The application can be configured and controlled using command-line arguments.

### Syntax

```bash
dotnet run -- [options] [initial prompt]
```

### Options

| Option                 | Description                                                                                                 | Example                                                 |
| ---------------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------------------- |
| `/help`                | Displays the help message with available commands and options.                                              | `dotnet run -- /help`                                   |
| `/model:<model_name>`  | Sets the OpenAI model to be used for the chat session. If not specified, the default model will be used.      | `dotnet run -- /model:gpt-4`                            |
| `/systemprompt:<prompt>` | Sets a custom system prompt for the chat session.                                                           | `dotnet run -- /systemprompt:"You are a pirate."`       |
| `/load:<file_name>`    | Loads a previously saved conversation from a JSON file.                                                     | `dotnet run -- /load:my_chat.json`                      |
| `/listmodels`          | Fetches and displays a list of available OpenAI models that can be used with the application.               | `dotnet run -- /listmodels`                             |
| `[initial prompt]`     | An optional initial prompt to start the conversation with. If the prompt contains spaces, enclose it in quotes. | `dotnet run -- "Tell me a joke about programmers."` |

### In-Chat Commands

Once the chat session has started, you can use the following commands:

| Command      | Description                                       |
| ------------ | ------------------------------------------------- |
| `/clear`     | Clears the current conversation history.          |
| `/save <file_name>` | Saves the current conversation to a JSON file.    |
| `/load <file_name>` | Loads a conversation from a JSON file.            |
| `/model <model_name>` | Changes the OpenAI model for the current session. |
| `/exit`      | Exits the application.                            |

## 3. Major Methods and Components

This section details the major methods and components of the solution.

### `Program.cs`

The `Program.cs` file contains the main entry point of the application and is responsible for the initial setup.

*   **`Main(string[] args)`**:
    *   Parses command-line arguments to configure the application settings (e.g., model, system prompt, initial prompt).
    *   Loads configuration from `appsettings.json` and environment variables.
    *   Initializes the `ChatService` and `ConsoleUI` components.
    *   Handles the `/help` and `/listmodels` commands.
    *   Starts the interactive chat session.

### `ChatService.cs`

The `ChatService` class encapsulates all the logic for interacting with the OpenAI API.

*   **`ChatService(string apiKey, string model, string systemPrompt)`**:
    *   The constructor initializes the `ChatClient` with the provided API key and model.
    *   It also sets up the initial conversation history with a system prompt.
*   **`GetChatResponseStreamingAsync(string userInput)`**:
    *   This method sends the user's input to the OpenAI API and streams the response back.
    *   It adds the user's message to the conversation history and then streams the assistant's response, updating the history as the response is received.
*   **`ClearMessages()`**:
    *   Clears the conversation history, preserving the initial system message.
*   **`LoadMessages(List<ChatMessage> messages)`**:
    *   Replaces the current conversation history with a new set of messages.
*   **`SetModel(string model)`**:
    *   Changes the model used for the chat session by creating a new `ChatClient` instance.
*   **`GetAvailableModelsAsync(string apiKey)`**:
    *   A static method that fetches the list of available "gpt" models from the OpenAI API.

### `ConsoleUI.cs`

The `ConsoleUI` class is responsible for managing the user interface in the console.

*   **`StartAsync()`**:
    *   The main loop of the UI, which continuously prompts the user for input and processes it.
*   **`HandleUserInput(string userInput)`**:
    *   Handles regular user messages by sending them to the `ChatService` and displaying the streaming response from the assistant.
    *   It also displays a "typing" indicator while waiting for the first part of the response.
*   **`HandleCommand(string userInput)`**:
    *   Parses and executes in-chat commands like `/clear`, `/save`, `/load`, `/model`, and `/exit`.
*   **`SaveConversation(string fileName)`**:
    *   Saves the current conversation to a file in JSON format using the `SerializableChatMessage` class.
*   **`LoadConversation(string fileName)`**:
    *   Loads a conversation from a JSON file and restores the chat history.

### `SerializableChatMessage.cs`

This class provides a way to serialize and deserialize `ChatMessage` objects, which are part of the OpenAI SDK and not directly serializable.

*   **`SerializableChatMessage(ChatMessage message)`**:
    *   A constructor that converts a `ChatMessage` object into a serializable format by storing its role and content.
*   **`ToChatMessage()`**:
    *   Converts a `SerializableChatMessage` object back into a `ChatMessage` object that can be used by the `ChatService`.
