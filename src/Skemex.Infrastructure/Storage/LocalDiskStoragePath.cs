using Microsoft.Extensions.Hosting;
using Skemex.Application.Configuration;

namespace Skemex.Infrastructure.Storage;

public static class LocalDiskStoragePath
{
    private const string DefaultTempSubfolder = "Skemex/storage";

    public static string ResolveRoot(IHostEnvironment hostEnvironment, LocalDiskStorageOptions local)
    {
        var raw = string.IsNullOrWhiteSpace(local.BasePath)
            ? Path.Combine(Path.GetTempPath(), DefaultTempSubfolder)
            : local.BasePath.Trim();

        if (Path.IsPathFullyQualified(raw))
        {
            return Path.GetFullPath(raw);
        }

        return Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, raw));
    }
}
