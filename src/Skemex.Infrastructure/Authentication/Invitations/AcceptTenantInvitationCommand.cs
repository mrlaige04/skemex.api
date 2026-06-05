using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class AcceptTenantInvitationCommand : ICommand<AcceptTenantInvitationResponse>
{
    public string Token { get; set; } = string.Empty;
    public string? Password { get; set; }
}
