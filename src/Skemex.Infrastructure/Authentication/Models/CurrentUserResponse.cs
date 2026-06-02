namespace Skemex.Infrastructure.Authentication.Models;

public class CurrentUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }

    /// <summary>Public URL for the profile photo (from <c>PhotoBlobId</c> and <c>Storage:PublicBlobBaseUrl</c>), if configured.</summary>
    public string? AvatarUrl { get; set; }
    public IList<TenantSummaryResponse> Tenants { get; set; } = [];
    public IList<RoleBaseResponse> Roles { get; set; } = [];
    public IList<PermissionBaseResponse> Permissions { get; set; } = [];
}
