namespace AgentRouter.models.chat;

public class ChatCompletionRequest
{
    public string? Model { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}