using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class GetTenantInvitationQuery : IQuery<TenantInvitationPreviewResponse>
{
    public string Token { get; set; } = string.Empty;
}
