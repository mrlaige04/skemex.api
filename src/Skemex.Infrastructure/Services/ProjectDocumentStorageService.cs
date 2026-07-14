using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

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
        var safeName = SanitizeFileName(fileName);
        return $"projects/{tenantId:N}/{projectId:N}/documents/{Guid.NewGuid():N}-{safeName}{ext}";
    }

    private static string SanitizeFileName(string? fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName ?? "document").Trim();
        if (baseName.Length == 0)
        {
            return "document";
        }

        var chars = baseName
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();
        var sanitized = new string(chars).Trim('-');
        return sanitized.Length == 0 ? "document" : sanitized[..Math.Min(sanitized.Length, 80)];
    }

    private static string NormalizeExtension(string? fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (ext is ".pdf" or ".docx" or ".png" or ".jpg" or ".jpeg")
        {
            return ext == ".jpeg" ? ".jpg" : ext;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => ".bin",
        };
    }
}
