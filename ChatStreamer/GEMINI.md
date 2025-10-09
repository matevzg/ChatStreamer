# Project Overview

This project is a .NET 9 console application that demonstrates how to create an interactive chat with the OpenAI API. It uses the official OpenAI .NET SDK to stream chat completions and maintain a conversation history.

## Building and Running

To build and run this project, you will need the .NET 9 SDK installed.

1.  **Set the API Key:**
    Set the `OPENAI_API_KEY` environment variable to your OpenAI API key.

    ```bash
    export OPENAI_API_KEY="your-api-key"
    ```

2.  **Run the application:**
    You can run the application using the `dotnet run` command from the `ChatStreamerApp` directory.

    ```bash
    cd ChatStreamerApp
    dotnet run
    ```
review 
    You can also pass an initial prompt to the application as a command-line argument:

    ```bash
    dotnet run -- "Hello, world!"
    ```

## Development Conventions

*   The project uses the latest C# features, including top-level statements and global using directives.
*   The code is well-commented and easy to understand.
*   The project follows the standard .NET project structure.
