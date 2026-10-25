using Skemex.Domain.Events.Abstractions;

namespace Skemex.Domain.Events.Users;

/// <summary>Raised when a pending workspace invitation becomes Active.</summary>
public sealed class TenantUserInvitationAcceptedDomainEvent : IDomainEvent
{
    public required Guid TenantUserId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string TenantName { get; init; }
}
