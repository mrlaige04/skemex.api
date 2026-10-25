namespace Skemex.Infrastructure.Authentication;

public static class CustomClaims
{
    public const string TenantId = "tenant-id";
    public const string IsSuperAdmin = "IsSuperAdmin";
    public const string Roles = "Roles";
    /// <summary>Legacy multi-claim type (one claim per permission). Prefer <see cref="Permissions"/>.</summary>
    public const string Permission = "permission";
    /// <summary>Space-separated permission names in a single claim (avoids JWT duplicate-key loss).</summary>
    public const string Permissions = "permissions";
}