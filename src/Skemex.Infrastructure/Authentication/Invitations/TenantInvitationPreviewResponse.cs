namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class TenantInvitationPreviewResponse
{
    public string TenantName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool RequiresPassword { get; set; }

    public bool IsExpired { get; set; }
}
