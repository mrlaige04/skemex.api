namespace Skemex.Application.Services.Users;

/// <summary>
/// Resolves the effective permission names for a user in a tenant from the database
/// (roles → role permissions). Source of truth for authorization — not JWT claims.
/// </summary>
public interface ITenantUserPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionNamesAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
