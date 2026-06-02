namespace Skemex.Domain.Services;

public interface ICurrentUser
{
    Guid? GetTenantId();
    Guid? GetUserId();
    string[]? GetRoles();
    void SetTenantId(Guid? tenantId);
    bool IsSuperAdmin();
}