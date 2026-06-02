using Skemex.Application.Services;

namespace Skemex.Infrastructure.Storage;

internal static class StorageBucketSegments
{
    public static string LocalFolderName(StorageBucketKind kind) =>
        kind switch
        {
            StorageBucketKind.Branding => "branding",
            StorageBucketKind.Files => "files",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
