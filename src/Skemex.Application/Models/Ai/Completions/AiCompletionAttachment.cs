namespace Skemex.Application.Models.Ai.Completions;

/// <summary>In-memory file payload for multimodal chat completions.</summary>
public sealed class AiCompletionAttachment
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
}
