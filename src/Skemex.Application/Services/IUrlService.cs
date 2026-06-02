namespace Skemex.Application.Services;

public interface IUrlService
{
    /// <summary>Public URL for profile photo keys in the branding bucket.</summary>
    string? GetUserProfilePictureUrl(string? photoBlobId);

    /// <summary>Public URL for tenant logo keys in the branding bucket.</summary>
    string? GetTenantLogoUrl(string? logoBlobId);

    /// <summary>Public URL for file keys in the files bucket (empty if <c>Storage:PublicFilesBlobBaseUrl</c> is unset).</summary>
    string? GetPublicFileBlobUrl(string? fileBlobId);
}
