using Microsoft.AspNetCore.Http;
using Skemex.Application.Configuration;

namespace Skemex.Infrastructure.Storage;

internal static class LocalBlobPublicUrlBuilder
{
    public static string Build(IHttpContextAccessor httpContextAccessor, StorageOptions options, string bucket, string path)
    {
        var normalizedPath = path.Trim().TrimStart('/');
        var prefix = (options.LocalPublicRequestPath ?? "/api/blobs").TrimEnd('/');
        if (!prefix.StartsWith('/'))
        {
            prefix = "/" + prefix;
        }

        var relative = $"{prefix}/{bucket}/{normalizedPath}";
        var http = httpContextAccessor.HttpContext?.Request;
        if (http is null)
        {
            return relative;
        }

        return $"{http.Scheme}://{http.Host}{relative}";
    }
}
