#nullable enable

using OpenAI.Chat;

public class SerializableChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }

    public SerializableChatMessage()
    {
        Role = string.Empty;
        Content = string.Empty;
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
        Content = message.Content?.ToString() ?? string.Empty;
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
