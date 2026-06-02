using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class PermissionGroup : TenantEntity
{
    public string Name { get; set; } = null!;
    public bool IsSystem { get; set; }

    public IList<Permission> Permissions = [];
}