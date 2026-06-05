using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class TenantUser : TenantEntity
{
    public User User { get; set; } = null!;
    public Guid UserId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public TenantUserStatus Status { get; set; } = TenantUserStatus.Pending;

    public string? InvitationToken { get; set; }

    public DateTimeOffset? InvitationTokenExpiresAt { get; set; }
}
