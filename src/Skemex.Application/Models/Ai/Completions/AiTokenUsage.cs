namespace Skemex.Application.Models.Ai.Completions;

/// <summary>Token usage returned by the provider when available.</summary>
public sealed class AiTokenUsage
{
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
}
