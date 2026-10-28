using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Helpers.Ai;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Storage;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Storage;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure.Services.Ai.Chat;

public sealed class AiChatAttachmentStore(
    IBlobStorageService blobStorage,
    IOptions<StorageOptions> storageOptions,
    ITenantRepository<AiChatMessageAttachment> attachmentRepository,
    ILogger<AiChatAttachmentStore> logger) : IAiChatAttachmentStore
{
    private readonly string _bucket = StorageBucketNames.Resolve(
        storageOptions.Value,
        StorageBucketKind.AiChatAttachments);

    public async Task<ErrorOr<IReadOnlyList<AiChatAttachmentRef>>> SaveFilesAsync(
        Guid tenantId,
        Guid chatId,
        Guid messageId,
        IReadOnlyList<AiChatAttachmentUpload> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return Array.Empty<AiChatAttachmentRef>();
        }

        if (files.Count > AiChatAttachmentRules.MaxFilesPerMessage)
        {
            return Error.Validation(
                "Ai.TooManyAttachments",
                $"You can attach at most {AiChatAttachmentRules.MaxFilesPerMessage} files per message.");
        }

        await blobStorage.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);

        var entities = new List<AiChatMessageAttachment>(files.Count);
        var refs = new List<AiChatAttachmentRef>(files.Count);

        foreach (var file in files)
        {
            var validationError = AiChatAttachmentRules.ValidateFile(
                file.FileName,
                file.ContentType,
                file.SizeBytes);
            if (validationError is not null)
            {
                return Error.Validation("Ai.InvalidAttachment", validationError);
            }

            var attachmentId = Guid.NewGuid();
            var safeName = SanitizeFileName(file.FileName);
            var contentType = AiChatAttachmentRules.ResolveContentType(file.FileName, file.ContentType);
            var storageKey = ObjectStoragePath.ValidateAndNormalize(
                $"tenants/{tenantId:D}/chats/{chatId:D}/messages/{messageId:D}/{attachmentId:D}/{safeName}");

            if (file.Content.CanSeek)
            {
                file.Content.Position = 0;
            }

            await blobStorage
                .UploadAsync(_bucket, storageKey, file.Content, contentType, cancellationToken)
                .ConfigureAwait(false);

            entities.Add(
                new AiChatMessageAttachment
                {
                    Id = attachmentId,
                    TenantId = tenantId,
                    ChatId = chatId,
                    MessageId = messageId,
                    FileName = safeName,
                    ContentType = contentType,
                    FileSizeBytes = file.SizeBytes,
                    BlobId = storageKey,
                });

            refs.Add(
                new AiChatAttachmentRef
                {
                    Id = attachmentId,
                    Kind = AiChatAttachmentKinds.File,
                    FileName = safeName,
                    ContentType = contentType,
                    SizeBytes = file.SizeBytes,
                    StorageKey = storageKey,
                    Bucket = _bucket,
                    ChatId = chatId,
                    MessageId = messageId,
                });
        }

        await attachmentRepository.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        return refs;
    }

    public async Task<IReadOnlyList<AiCompletionAttachment>> LoadForCompletionAsync(
        IReadOnlyList<AiChatAttachmentRef> refs,
        CancellationToken cancellationToken = default)
    {
        var result = new List<AiCompletionAttachment>();
        foreach (var entry in refs.Where(item =>
                     string.Equals(item.Kind, AiChatAttachmentKinds.File, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(item.StorageKey)))
        {
            var bucket = string.IsNullOrWhiteSpace(entry.Bucket) ? _bucket : entry.Bucket!;
            try
            {
                var (stream, contentType) = await blobStorage
                    .DownloadAsync(bucket, entry.StorageKey!, cancellationToken)
                    .ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                    result.Add(
                        new AiCompletionAttachment
                        {
                            FileName = entry.FileName,
                            ContentType = string.IsNullOrWhiteSpace(contentType)
                                ? entry.ContentType
                                : contentType,
                            Data = memory.ToArray(),
                        });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to load AI chat attachment {AttachmentId} ({FileName}).",
                    entry.Id,
                    entry.FileName);
            }
        }

        return result;
    }

    public Task DeleteBlobsForChatAsync(Guid chatId, CancellationToken cancellationToken = default) =>
        DeleteBlobsAsync(
            filter: entry => entry.ChatId == chatId,
            cancellationToken);

    public Task DeleteBlobsForMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        DeleteBlobsAsync(
            filter: entry => entry.MessageId == messageId,
            cancellationToken);

    private async Task DeleteBlobsAsync(
        System.Linq.Expressions.Expression<Func<AiChatMessageAttachment, bool>> filter,
        CancellationToken cancellationToken)
    {
        var attachments = await attachmentRepository
            .GetAllAsync(filter: filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (attachments.Count == 0)
        {
            return;
        }

        await blobStorage.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.BlobId))
            {
                continue;
            }

            try
            {
                await blobStorage
                    .DeleteAsync(_bucket, attachment.BlobId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete AI chat attachment blob {BlobId} ({FileName}).",
                    attachment.BlobId,
                    attachment.FileName);
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        var stem = ObjectStoragePath.SanitizeSegment(
            Path.GetFileNameWithoutExtension(name),
            maxLength: 160,
            fallback: "attachment");
        var ext = NormalizeExtension(Path.GetExtension(name));
        return stem + ext;
    }

    private static string NormalizeExtension(string? extension)
    {
        var raw = (extension ?? string.Empty).Trim();
        if (raw.Length is 0 or > 16)
        {
            return string.Empty;
        }

        var withoutDot = raw.TrimStart('.');
        var safe = ObjectStoragePath.SanitizeSegment(withoutDot, maxLength: 16, fallback: string.Empty);
        return string.IsNullOrEmpty(safe) ? string.Empty : $".{safe}";
    }
}
