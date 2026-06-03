namespace Skemex.Application.Services;

public interface IBlobStorageService
{
    Task EnsureBucketExistsAsync(string bucket, CancellationToken cancellationToken = default);

    Task UploadAsync(string bucket, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType)> DownloadAsync(string bucket, string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string bucket, string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string bucket, string storageKey, CancellationToken cancellationToken = default);

    Task<string> GetPresignedDownloadUrlAsync(
        string bucket,
        string storageKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}
