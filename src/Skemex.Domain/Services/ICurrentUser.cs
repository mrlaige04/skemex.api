namespace Skemex.Domain.Services;

public interface ICurrentUser
{
    Guid? GetTenantId();
    Guid? GetUserId();
    string[]? GetRoles();
    string[] GetPermissions();
    bool HasPermission(string permission);
    void SetTenantId(Guid? tenantId);
    bool IsSuperAdmin();
}
