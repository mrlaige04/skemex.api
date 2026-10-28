using ErrorOr;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;

namespace Skemex.Application.Services.Ai.Chat;

public interface IAiChatAttachmentStore
{
    /// <summary>
    /// Uploads files to the AI chat attachments bucket and persists rows linked to the message.
    /// </summary>
    Task<ErrorOr<IReadOnlyList<AiChatAttachmentRef>>> SaveFilesAsync(
        Guid tenantId,
        Guid chatId,
        Guid messageId,
        IReadOnlyList<AiChatAttachmentUpload> files,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiCompletionAttachment>> LoadForCompletionAsync(
        IReadOnlyList<AiChatAttachmentRef> refs,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes blob objects for every attachment on the chat (DB rows cascade separately).</summary>
    Task DeleteBlobsForChatAsync(Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Deletes blob objects for every attachment on the message (DB rows cascade separately).</summary>
    Task DeleteBlobsForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
