namespace Skemex.Application.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = StorageProviderNames.Local;
    public string? PublicBlobBaseUrl { get; set; }
    public string? PublicBrandingBlobBaseUrl { get; set; }
    public string? PublicFilesBlobBaseUrl { get; set; }
    public string LocalPublicRequestPath { get; set; } = "/api/blobs";

    public LocalDiskStorageOptions Local { get; set; } = new();
    public MinioStorageOptions Minio { get; set; } = new();
}

public static class StorageProviderNames
{
    public const string Local = "Local";
    public const string Production = "Production";
}

public sealed class LocalDiskStorageOptions
{
    /// <summary>
    /// Root directory for local blobs. When empty, uses
    /// <c>%TEMP%/Skemex/storage</c> (<see cref="Path.GetTempPath"/>).
    /// Absolute paths are used as-is; relative paths are under the app content root.
    /// </summary>
    public string BasePath { get; set; } = string.Empty;
}

public sealed class MinioStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public string BrandingBucket { get; set; } = string.Empty;
    public string FilesBucket { get; set; } = string.Empty;
}
