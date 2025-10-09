
#nullable enable

using OpenAI.Chat;
using System;
using System.Linq;

public class SerializableChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }

    public SerializableChatMessage()
    {
        Role = string.Empty;
        Content = string.Empty;
        Timestamp = DateTime.UtcNow;
    }

    public SerializableChatMessage(ChatMessage message)
    {
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

