using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Models.Ai.Chat;

public sealed class AiChatMessageAttachmentDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public static AiChatMessageAttachmentDto FromEntity(AiChatMessageAttachment attachment) =>
        new()
        {
            Id = attachment.Id,
            ChatId = attachment.ChatId,
            MessageId = attachment.MessageId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            CreatedAt = attachment.CreatedAt,
        };
}
