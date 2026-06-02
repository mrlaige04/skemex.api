using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Storage;

public sealed class MinioBlobStorageService(IMinioClient client, IOptions<StorageOptions> options) : IStorageService
{
    private readonly IMinioClient _client = client;
    private readonly MinioStorageOptions _minio = options.Value.Minio;

    private string Bucket(StorageBucketKind kind) =>
        kind switch
        {
            StorageBucketKind.Branding => _minio.BrandingBucket,
            StorageBucketKind.Files => _minio.FilesBucket,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public async Task UploadAsync(StorageBucketKind bucket, string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        var (stream, disposeStream) = await MaterializeStreamAsync(content, cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(Bucket(bucket))
                    .WithObject(key)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(contentType),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (disposeStream)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<(Stream Stream, string ContentType)> DownloadAsync(StorageBucketKind bucket, string objectKey,
        CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        var bucketName = Bucket(bucket);

        ObjectStat stat;
        try
        {
            stat = await _client.StatObjectAsync(
                new StatObjectArgs().WithBucket(bucketName).WithObject(key),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException ex)
        {
            throw new FileNotFoundException("Blob not found.", key, ex);
        }

        var ms = new MemoryStream();
        try
        {
            await _client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(key)
                    .WithCallbackStream(stream => stream.CopyTo(ms)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException ex)
        {
            await ms.DisposeAsync().ConfigureAwait(false);
            throw new FileNotFoundException("Blob not found.", key, ex);
        }

        ms.Position = 0;
        var contentType = string.IsNullOrWhiteSpace(stat.ContentType)
            ? "application/octet-stream"
            : stat.ContentType;
        return (ms, contentType);
    }

    public async Task DeleteAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(Bucket(bucket)).WithObject(key),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException)
        {
            // treat as already gone
        }
    }

    public async Task<bool> ExistsAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        try
        {
            await _client.StatObjectAsync(
                new StatObjectArgs().WithBucket(Bucket(bucket)).WithObject(key),
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }

    private static async Task<(Stream Stream, bool DisposeStream)> MaterializeStreamAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            return (content, false);
        }

        var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        return (ms, true);
    }
}
