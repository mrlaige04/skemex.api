using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class ProjectDocumentStorageService(
    IBlobStorageService blobs,
    IOptions<StorageOptions> storageOptions) : IProjectDocumentStorageService
{
    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly string _bucket = StorageBucketNames.Resolve(
        storageOptions.Value,
        StorageBucketKind.ProjectDocuments);

    public async Task<string> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);

        var storageKey = BuildStorageKey(tenantId, projectId, fileName, contentType);
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
        var publicBase = _storage.PublicProjectDocumentsBlobBaseUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(publicBase))
        {
            return $"{publicBase}/{path}";
        }

        var expiry = ResolvePresignedExpiry();
        return await blobs
            .GetPresignedDownloadUrlAsync(_bucket, path, expiry, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        await blobs.EnsureBucketExistsAsync(_bucket, cancellationToken).ConfigureAwait(false);
        var (stream, _) = await blobs
            .DownloadAsync(_bucket, storageKey.Trim().TrimStart('/'), cancellationToken)
            .ConfigureAwait(false);
        return stream;
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

    private static string BuildStorageKey(Guid tenantId, Guid projectId, string? fileName, string? contentType)
    {
        var ext = NormalizeExtension(fileName, contentType);
        var safeName = ObjectStoragePath.SanitizeSegment(
            Path.GetFileNameWithoutExtension(fileName ?? "document"),
            fallback: "document");
        var key = $"projects/{tenantId:N}/{projectId:N}/documents/{Guid.NewGuid():N}-{safeName}{ext}";
        return ObjectStoragePath.ValidateAndNormalize(key);
    }

    private static string NormalizeExtension(string? fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (ext is ".pdf" or ".docx" or ".png" or ".jpg" or ".jpeg" or ".txt" or ".md")
        {
            return ext == ".jpeg" ? ".jpg" : ext;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "text/plain" => ".txt",
            "text/markdown" => ".md",
            _ => ".bin",
        };
    }
}
