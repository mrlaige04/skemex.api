using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class StorageUrlService(IOptions<StorageOptions> options, IHttpContextAccessor httpContextAccessor)
    : IUrlService
{
    private readonly StorageOptions _options = options.Value;

    public string? GetUserProfilePictureUrl(string? photoBlobId) =>
        BuildBrandingUrl(photoBlobId);

    public string? GetTenantLogoUrl(string? logoBlobId) =>
        BuildBrandingUrl(logoBlobId);

    public string? GetPublicFileBlobUrl(string? fileBlobId) =>
        BuildFilesUrl(fileBlobId);

    private string? BuildBrandingUrl(string? blobId)
    {
        if (string.IsNullOrWhiteSpace(blobId))
        {
            return null;
        }

        var path = blobId.Trim().TrimStart('/');
        var baseUrl = (_options.PublicBrandingBlobBaseUrl ?? _options.PublicBlobBaseUrl)?.TrimEnd('/');

        if (!string.IsNullOrEmpty(baseUrl))
        {
            return $"{baseUrl}/{path}";
        }

        if (string.Equals(_options.Provider, StorageProviderNames.Local, StringComparison.OrdinalIgnoreCase))
        {
            return BuildLocalBlobUrl(StorageBucketKind.Branding, path);
        }

        return null;
    }

    private string? BuildFilesUrl(string? blobId)
    {
        if (string.IsNullOrWhiteSpace(blobId))
        {
            return null;
        }

        var path = blobId.Trim().TrimStart('/');
        var baseUrl = _options.PublicFilesBlobBaseUrl?.TrimEnd('/');

        if (!string.IsNullOrEmpty(baseUrl))
        {
            return $"{baseUrl}/{path}";
        }

        if (string.Equals(_options.Provider, StorageProviderNames.Local, StringComparison.OrdinalIgnoreCase))
        {
            return BuildLocalBlobUrl(StorageBucketKind.Files, path);
        }

        return null;
    }

    private string? BuildLocalBlobUrl(StorageBucketKind bucket, string path)
    {
        var segment = StorageBucketSegments.LocalFolderName(bucket);
        var prefix = (_options.LocalPublicRequestPath ?? "/api/blobs").TrimEnd('/');
        if (!prefix.StartsWith('/'))
        {
            prefix = "/" + prefix;
        }

        var relative = $"{prefix}/{segment}/{path}";
        var http = httpContextAccessor.HttpContext?.Request;
        if (http is null)
        {
            return relative;
        }

        return $"{http.Scheme}://{http.Host}{relative}";
    }
}
