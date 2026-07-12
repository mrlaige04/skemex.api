using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using StorageBucketKind = Skemex.Application.Services.StorageBucketKind;

namespace Skemex.Infrastructure.Services;

public sealed class ProjectLogoService(
    IBlobStorageService blobs,
    IOptions<StorageOptions> storageOptions) : IProjectLogoService
{
    private readonly string _bucket = StorageBucketNames.Resolve(storageOptions.Value, StorageBucketKind.Branding);

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

    private static string BuildStorageKey(Guid tenantId, Guid projectId, string? fileName, string? contentType)
    {
        var ext = NormalizeExtension(fileName, contentType);
        return $"projects/{tenantId:N}/{projectId:N}/logo-{Guid.NewGuid():N}{ext}";
    }

    private static string NormalizeExtension(string? fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")
        {
            return ext;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg",
        };
    }
}
