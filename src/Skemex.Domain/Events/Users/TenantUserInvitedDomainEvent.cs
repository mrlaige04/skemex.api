using Skemex.Domain.Events.Abstractions;

namespace Skemex.Domain.Events.Users;

/// <summary>Raised when a workspace membership invitation is created or resent.</summary>
public sealed class TenantUserInvitedDomainEvent : IDomainEvent
{
    public required Guid TenantUserId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string InvitationToken { get; init; }
}
