using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class GetTenantInvitationHandler(
    UserManager<User> userManager,
    IBaseRepository<TenantUser> tenantUserRepository)
    : IQueryHandler<GetTenantInvitationQuery, TenantInvitationPreviewResponse>
{
    public async Task<ErrorOr<TenantInvitationPreviewResponse>> Handle(
        GetTenantInvitationQuery request,
        CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var tenantUser = await tenantUserRepository.GetAsync(
            tu => tu.InvitationToken == token,
            include: q => q.Include(tu => tu.Tenant).Include(tu => tu.User),
            cancellationToken: cancellationToken);

        if (tenantUser is null || tenantUser.Status != TenantUserStatus.Pending)
        {
            return Error.NotFound("Invitation.NotFound", "Invitation was not found or has already been used.");
        }

        var user = tenantUser.User;
        var isExpired = tenantUser.InvitationTokenExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow;

        return new TenantInvitationPreviewResponse
        {
            TenantName = tenantUser.Tenant.Name,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            RequiresPassword = !await userManager.HasPasswordAsync(user).ConfigureAwait(false),
            IsExpired = isExpired,
        };
    }
}
