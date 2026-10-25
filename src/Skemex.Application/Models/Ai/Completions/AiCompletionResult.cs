namespace Skemex.Application.Models.Ai.Completions;

public sealed class AiCompletionResult
{
    public required string Content { get; init; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public AiTokenUsage? Usage { get; set; }
}
