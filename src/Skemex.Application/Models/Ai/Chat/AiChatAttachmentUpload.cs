namespace Skemex.Application.Models.Ai.Chat;

/// <summary>Uploaded file payload before it is stored for an AI job.</summary>
public sealed class AiChatAttachmentUpload
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required Stream Content { get; init; }
}
