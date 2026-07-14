using Skemex.Application.Services;

namespace Skemex.Application.Configuration;

public static class StorageBucketNames
{
    public static string Resolve(StorageOptions options, StorageBucketKind kind)
    {
        if (string.Equals(options.Provider, StorageProviderNames.Production, StringComparison.OrdinalIgnoreCase))
        {
            return kind switch
            {
                StorageBucketKind.Branding => options.Minio.BrandingBucket,
                StorageBucketKind.Files => options.Minio.FilesBucket,
                StorageBucketKind.ProjectDocuments => options.Minio.ProjectDocumentsBucket,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }

        return kind switch
        {
            StorageBucketKind.Branding => "branding",
            StorageBucketKind.Files => "files",
            StorageBucketKind.ProjectDocuments => "project-documents",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
