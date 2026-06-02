using Microsoft.AspNetCore.Identity;
using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class Role : IdentityRole<Guid>, IEntity<Guid>, IAuditableEntity
{
    public bool IsSystem { get; set; }

    public Tenant? Tenant { get; set; }
    public Guid? TenantId { get; set; }

    public IList<UserRole> UserRoles { get; set; } = [];
    public IList<RolePermission> Permissions { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}