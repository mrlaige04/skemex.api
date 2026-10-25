namespace Skemex.Application.Models.Ai.Completions;

/// <summary>Chat + function-calling request. Used by the UI chat flow.</summary>
public sealed class AiFunctionCallRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string Model { get; init; }
    public required IReadOnlyList<AiToolDefinition> Tools { get; init; }
}
