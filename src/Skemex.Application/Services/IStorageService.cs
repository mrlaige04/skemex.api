namespace Skemex.Application.Services;

public interface IStorageService
{
    Task UploadAsync(StorageBucketKind bucket, string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType)> DownloadAsync(StorageBucketKind bucket, string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default);
}
