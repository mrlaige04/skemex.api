using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Services;

public sealed class ProfileImageService(
    IBlobStorageService blobs,
    IOptions<ProfileImageStorageOptions> options) : IProfileImageService
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task<string> CreateAsync(
        Guid userId,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken).ConfigureAwait(false);

        var storageKey = BuildStorageKey(userId, fileName, contentType);
        content.Position = 0;
        await blobs.UploadAsync(_bucket, storageKey, content, contentType, cancellationToken)
            .ConfigureAwait(false);
        return storageKey;
    }

    public async Task<string> ReplaceAsync(
        Guid userId,
        string? previousStorageKey,
        Stream content,
        string contentType,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken).ConfigureAwait(false);

        var storageKey = await CreateAsync(userId, content, contentType, fileName, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(previousStorageKey)
            && !string.Equals(previousStorageKey, storageKey, StringComparison.Ordinal))
        {
            try
            {
                await blobs.DeleteAsync(_bucket, previousStorageKey, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                /* best-effort cleanup */
            }
        }

        return storageKey;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken).ConfigureAwait(false);
        await blobs.DeleteAsync(_bucket, storageKey, cancellationToken).ConfigureAwait(false);
    }

    private Task EnsureBucketExistsAsync(CancellationToken cancellationToken) =>
        blobs.EnsureBucketExistsAsync(_bucket, cancellationToken);

    private static string BuildStorageKey(Guid userId, string? fileName, string? contentType)
    {
        var ext = NormalizeExtension(fileName, contentType);
        return $"users/{userId:N}/profile-{Guid.NewGuid():N}{ext}";
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
