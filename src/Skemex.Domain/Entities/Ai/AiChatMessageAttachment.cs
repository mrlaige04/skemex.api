using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Ai;

/// <summary>File (or future artefact) attachment on an AI chat message.</summary>
public class AiChatMessageAttachment : TenantEntity
{
    public Guid ChatId { get; set; }
    public AiChat Chat { get; set; } = null!;

    public Guid MessageId { get; set; }
    public AiChatMessage Message { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>Object storage key within the AI chat attachments bucket.</summary>
    public string BlobId { get; set; } = string.Empty;
}
