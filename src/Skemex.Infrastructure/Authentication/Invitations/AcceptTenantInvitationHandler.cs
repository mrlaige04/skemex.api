using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class AcceptTenantInvitationHandler(
    UserManager<User> userManager,
    IBaseRepository<TenantUser> tenantUserRepository)
    : ICommandHandler<AcceptTenantInvitationCommand, AcceptTenantInvitationResponse>
{
    public async Task<ErrorOr<AcceptTenantInvitationResponse>> Handle(
        AcceptTenantInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var token = request.Token.Trim();
        var tenantUser = await tenantUserRepository.GetAsync(
            tu => tu.InvitationToken == token,
            include: q => q.Include(tu => tu.Tenant).Include(tu => tu.User),
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("Invitation.NotFound", "Invitation was not found or has already been used.");
        }

        if (tenantUser.Status != TenantUserStatus.Pending)
        {
            return Error.Validation("Invitation.NotPending", "This invitation is no longer pending.");
        }

        if (tenantUser.InvitationTokenExpiresAt is { } expiresAt && expiresAt < DateTimeOffset.UtcNow)
        {
            return Error.Validation("Invitation.Expired", "This invitation has expired.");
        }

        var user = tenantUser.User;
        if (!await userManager.HasPasswordAsync(user))
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Error.Validation(
                    "Invitation.PasswordRequired",
                    "Set a password to activate your account and accept the invitation.");
            }

            var addPassword = await userManager.AddPasswordAsync(user, request.Password);
            if (!addPassword.Succeeded)
            {
                return Error.Validation(
                    "Invitation.PasswordInvalid",
                    string.Join(' ', addPassword.Errors.Select(e => e.Description)));
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
            }
        }

        tenantUser.Status = TenantUserStatus.Active;
        tenantUser.InvitationToken = null;
        tenantUser.InvitationTokenExpiresAt = null;

        await tenantUserRepository.UpdateAsync(tenantUser, cancellationToken);

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return new AcceptTenantInvitationResponse
        {
            TenantId = tenantUser.TenantId,
            TenantName = tenantUser.Tenant.Name,
        };
    }
}
