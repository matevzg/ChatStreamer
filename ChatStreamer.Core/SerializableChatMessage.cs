
#nullable enable

using OpenAI.Chat;
using System;
using System.Linq;

/// <summary>
/// Provides a JSON-serializable wrapper for OpenAI ChatMessage objects.
/// Enables saving and loading conversations to/from JSON files.
/// </summary>
public class SerializableChatMessage
{
    /// <summary>
    /// Gets or sets the role of the message sender ("System", "User", or "Assistant").
    /// </summary>
    public string Role { get; set; }
    
    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public string Content { get; set; }
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Initializes a new instance of the SerializableChatMessage class with default values.
    /// </summary>
    public SerializableChatMessage()
    {
        Role = string.Empty;
        Content = string.Empty;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of the SerializableChatMessage class from an OpenAI ChatMessage.
    /// </summary>
    /// <param name="message">The ChatMessage to convert.</param>
    /// <exception cref="NotSupportedException">Thrown when the message type is not supported.</exception>
    public SerializableChatMessage(ChatMessage message)
    {
        // Map ChatMessage type to role string
        Role = message switch
        {
            SystemChatMessage => "System",
            UserChatMessage => "User",
            AssistantChatMessage => "Assistant",
            _ => throw new NotSupportedException("Unsupported message type.")
        };
        Content = string.Join(Environment.NewLine, message.Content.Select(part => part.Text));
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Converts this serializable message back to an OpenAI ChatMessage.
    /// </summary>
    /// <returns>A ChatMessage instance with the appropriate type based on the role.</returns>
    /// <exception cref="NotSupportedException">Thrown when the role is not recognized.</exception>
    public ChatMessage ToChatMessage()
    {
        return Role switch
        {
            "System" => ChatMessage.CreateSystemMessage(Content),
            "User" => ChatMessage.CreateUserMessage(Content),
            "Assistant" => ChatMessage.CreateAssistantMessage(Content),
            _ => throw new NotSupportedException("Unsupported role.")
        };
    }
}

