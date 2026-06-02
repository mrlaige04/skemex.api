using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class TenantUser : TenantEntity
{
    public User User { get; set; } = null!;
    public Guid UserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}