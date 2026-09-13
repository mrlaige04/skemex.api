using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class IssueAttachmentStorageService(
    IBlobStorageService blobs,
    IOptions<StorageOptions> storageOptions) : IIssueAttachmentStorageService
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly string _bucket = StorageBucketNames.Resolve(
        storageOptions.Value,
        StorageBucketKind.IssueAttachments);

    public async Task<string> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Guid taskId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);

        var storageKey = BuildStorageKey(tenantId, projectId, taskId, fileName);
        content.Position = 0;
        await blobs.UploadAsync(_bucket, storageKey, content, contentType, cancellationToken)
            .ConfigureAwait(false);
        return storageKey;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);
        await blobs.DeleteAsync(_bucket, storageKey, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetDownloadUrlAsync(
        string? storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        var path = storageKey.Trim().TrimStart('/');
        var publicBase = _storage.PublicIssueAttachmentsBlobBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(publicBase))
        {
            return $"{publicBase}/{path}";
        }

        var expiry = ResolvePresignedExpiry();
        return await blobs
            .GetPresignedDownloadUrlAsync(_bucket, path, expiry, cancellationToken)
            .ConfigureAwait(false);
    }

    private TimeSpan ResolvePresignedExpiry()
    {
        var seconds = _storage.Minio.PresignedDownloadExpirySeconds;
        if (seconds <= 0)
        {
            seconds = 3600;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string BuildStorageKey(Guid tenantId, Guid projectId, Guid taskId, string? fileName)
    {
        var ext = NormalizeExtension(fileName);
        var safeName = ObjectStoragePath.SanitizeSegment(
            Path.GetFileNameWithoutExtension(fileName ?? "attachment"),
            fallback: "attachment");
        var key =
            $"projects/{tenantId:N}/{projectId:N}/tasks/{taskId:N}/attachments/{Guid.NewGuid():N}-{safeName}{ext}";
        return ObjectStoragePath.ValidateAndNormalize(key);
    }

    private static string NormalizeExtension(string? fileName)
    {
        var raw = Path.GetExtension(fileName ?? string.Empty).Trim();
        if (raw.Length is 0 or > 32)
        {
            return string.Empty;
        }

        var withoutDot = raw.TrimStart('.');
        var safe = ObjectStoragePath.SanitizeSegment(withoutDot, maxLength: 16, fallback: string.Empty);
        return string.IsNullOrEmpty(safe) ? string.Empty : $".{safe}";
    }
}
