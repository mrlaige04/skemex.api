namespace Skemex.Application.Models.Ai;

public sealed class AiChatRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public string? Model { get; init; }
}

public sealed class AiChatResult
{
    public required string Text { get; init; }
}
