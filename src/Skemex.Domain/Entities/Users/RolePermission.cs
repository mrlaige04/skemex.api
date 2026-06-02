using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class RolePermission : TenantEntity
{
    public Role Role { get; set; } = null!;
    public Guid RoleId { get; set; }

    public Permission Permission { get; set; } = null!;
    public Guid PermissionId { get; set; }
}