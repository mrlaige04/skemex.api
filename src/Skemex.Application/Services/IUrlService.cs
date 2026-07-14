namespace Skemex.Application.Services;

public interface IUrlService
{
    Task<string?> GetUserProfilePictureUrlAsync(string? photoBlobId, CancellationToken cancellationToken = default);
    string? GetTenantLogoUrl(string? logoBlobId);
    string? GetProjectLogoUrl(string? logoBlobId);
    string? GetPublicFileBlobUrl(string? fileBlobId);
    string? GetProjectDocumentUrl(string? blobId);
}
