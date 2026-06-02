using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Storage;

public sealed class LocalDiskStorageService(IOptions<StorageOptions> options, IHostEnvironment hostEnvironment)
    : IStorageService
{
    private readonly string _root = LocalDiskStoragePath.ResolveRoot(hostEnvironment, options.Value.Local);

    public async Task UploadAsync(StorageBucketKind bucket, string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        var fullPath = GetPhysicalPath(bucket, key);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                       FileOptions.Asynchronous))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        await File.WriteAllTextAsync(MetaPath(fullPath), contentType, cancellationToken);
    }

    public Task<(Stream Stream, string ContentType)> DownloadAsync(StorageBucketKind bucket, string objectKey,
        CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        if (!TryResolveExistingPath(bucket, key, out var fullPath) || fullPath is null)
        {
            throw new FileNotFoundException("Blob not found.", key);
        }

        var contentType = ResolveContentType(fullPath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous);
        return Task.FromResult((stream, contentType));
    }

    public Task DeleteAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        DeleteOne(GetPhysicalPath(bucket, key));
        if (bucket == StorageBucketKind.Branding)
        {
            var legacy = LegacyFlatBrandingPath(key);
            if (legacy is not null)
            {
                DeleteOne(legacy);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(StorageBucketKind bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var key = ObjectStoragePath.ValidateAndNormalize(objectKey);
        return Task.FromResult(TryResolveExistingPath(bucket, key, out _));
    }

    private static void DeleteOne(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        var meta = MetaPath(fullPath);
        if (File.Exists(meta))
        {
            File.Delete(meta);
        }
    }

    private bool TryResolveExistingPath(StorageBucketKind bucket, string normalizedKey, out string? fullPath)
    {
        fullPath = GetPhysicalPath(bucket, normalizedKey);
        if (File.Exists(fullPath))
        {
            return true;
        }

        if (bucket == StorageBucketKind.Branding)
        {
            var legacy = LegacyFlatBrandingPath(normalizedKey);
            if (legacy is not null && File.Exists(legacy))
            {
                fullPath = legacy;
                return true;
            }
        }

        fullPath = null;
        return false;
    }

    private string? LegacyFlatBrandingPath(string normalizedKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_root,
            normalizedKey.Replace('/', Path.DirectorySeparatorChar)));
        var rel = Path.GetRelativePath(_root, combined);
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
        {
            return null;
        }

        return combined;
    }

    private string GetPhysicalPath(StorageBucketKind bucket, string normalizedKey)
    {
        if (!Directory.Exists(_root))
        {
            Directory.CreateDirectory(_root);
        }

        var prefix = StorageBucketSegments.LocalFolderName(bucket);
        var relative = Path.Combine(prefix, normalizedKey.Replace('/', Path.DirectorySeparatorChar));
        var combined = Path.GetFullPath(Path.Combine(_root, relative));
        var rel = Path.GetRelativePath(_root, combined);
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
        {
            throw new InvalidOperationException("Resolved path escapes storage root.");
        }

        return combined;
    }

    private static string MetaPath(string dataFilePath) => dataFilePath + ".contentType";

    private static string ResolveContentType(string fullPath)
    {
        var meta = MetaPath(fullPath);
        if (File.Exists(meta))
        {
            return File.ReadAllText(meta);
        }

        return GuessContentTypeFromExtension(fullPath);
    }

    private static string GuessContentTypeFromExtension(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };
    }
}
