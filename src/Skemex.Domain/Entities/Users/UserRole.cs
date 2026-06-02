using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class UserRole : BaseEntity
{
    public User User { get; set; } = null!;
    public Guid UserId { get; set; }

    public Role Role { get; set; } = null!;
    public Guid RoleId { get; set; }

    public Tenant? Tenant { get; set; }
    public Guid? TenantId { get; set; }
}