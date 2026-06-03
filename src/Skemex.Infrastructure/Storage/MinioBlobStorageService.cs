using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Storage;

public sealed class MinioBlobStorageService : IBlobStorageService
{
    private readonly IMinioClient _client;
    private readonly Lazy<IMinioClient> _presignClient;

    public MinioBlobStorageService(
        IMinioClient client,
        IOptions<StorageOptions> storageOptions,
        IConfiguration configuration)
    {
        _client = client;
        var storage = storageOptions.Value;
        _presignClient = new Lazy<IMinioClient>(() =>
        {
            var endpoint = string.IsNullOrWhiteSpace(storage.Minio.PublicEndpoint)
                ? storage.Minio.Endpoint
                : storage.Minio.PublicEndpoint!;
            return SkemexMinioClientFactory.Create(storage, configuration, endpoint);
        });
    }

    public async Task EnsureBucketExistsAsync(string bucket, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);

        var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket),
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UploadAsync(string bucket, string storageKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        var key = ObjectStoragePath.ValidateAndNormalize(storageKey);
        var (stream, disposeStream) = await MaterializeStreamAsync(content, cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(bucket)
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

    public async Task<(Stream Stream, string ContentType)> DownloadAsync(string bucket, string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        var key = ObjectStoragePath.ValidateAndNormalize(storageKey);

        ObjectStat stat;
        try
        {
            stat = await _client.StatObjectAsync(
                new StatObjectArgs().WithBucket(bucket).WithObject(key),
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
                    .WithBucket(bucket)
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

    public async Task DeleteAsync(string bucket, string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        var key = ObjectStoragePath.ValidateAndNormalize(storageKey);
        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(bucket).WithObject(key),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException)
        {
            // treat as already gone
        }
    }

    public async Task<bool> ExistsAsync(string bucket, string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        var key = ObjectStoragePath.ValidateAndNormalize(storageKey);
        try
        {
            await _client.StatObjectAsync(
                new StatObjectArgs().WithBucket(bucket).WithObject(key),
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string bucket,
        string storageKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        var key = ObjectStoragePath.ValidateAndNormalize(storageKey);
        var seconds = (int)Math.Clamp(expiry.TotalSeconds, 1, 7 * 24 * 3600);

        cancellationToken.ThrowIfCancellationRequested();

        return await _presignClient.Value.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(key)
                .WithExpiry(seconds)).ConfigureAwait(false);
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
