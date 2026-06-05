namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class AcceptTenantInvitationResponse
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;
}
