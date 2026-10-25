using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Infrastructure.Storage;
using Skemex.Application.Models.Storage;

namespace Skemex.Infrastructure.Services;

public sealed class StorageUrlService(
    IOptions<StorageOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IProfileImageService profileImages,
    IProjectDocumentStorageService projectDocuments,
    IIssueAttachmentStorageService issueAttachments) : IUrlService
{
    private readonly StorageOptions _options = options.Value;

    public Task<string?> GetUserProfilePictureUrlAsync(string? photoBlobId, CancellationToken cancellationToken = default) =>
        profileImages.GetAvatarUrlAsync(photoBlobId, cancellationToken);

    public string? GetTenantLogoUrl(string? logoBlobId) =>
        BuildBrandingUrl(logoBlobId);

    public string? GetProjectLogoUrl(string? logoBlobId) =>
        BuildBrandingUrl(logoBlobId);

    public string? GetPublicFileBlobUrl(string? fileBlobId) =>
        BuildFilesUrl(fileBlobId);

    public Task<string?> GetProjectDocumentUrlAsync(string? blobId, CancellationToken cancellationToken = default) =>
        projectDocuments.GetDownloadUrlAsync(blobId, cancellationToken);

    public Task<string?> GetIssueAttachmentUrlAsync(string? blobId, CancellationToken cancellationToken = default) =>
        issueAttachments.GetDownloadUrlAsync(blobId, cancellationToken);

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
