namespace Skemex.Application.Models.Ai.Completions;

/// <summary>Plain text/JSON completion (no tools). Used by agent tools in direct execution.</summary>
public sealed class AiCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string Model { get; init; }
    public bool PreferJsonObject { get; init; } = true;
}
