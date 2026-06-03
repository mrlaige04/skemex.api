using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Infrastructure.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class StorageUrlService(
    IOptions<StorageOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IProfileImageService profileImages) : IUrlService
{
    private readonly StorageOptions _options = options.Value;

    public Task<string?> GetUserProfilePictureUrlAsync(string? photoBlobId, CancellationToken cancellationToken = default) =>
        profileImages.GetAvatarUrlAsync(photoBlobId, cancellationToken);

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
            var bucket = StorageBucketNames.Resolve(_options, StorageBucketKind.Branding);
            return LocalBlobPublicUrlBuilder.Build(httpContextAccessor, _options, bucket, path);
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
            var bucket = StorageBucketNames.Resolve(_options, StorageBucketKind.Files);
            return LocalBlobPublicUrlBuilder.Build(httpContextAccessor, _options, bucket, path);
        }

        return null;
    }
}
