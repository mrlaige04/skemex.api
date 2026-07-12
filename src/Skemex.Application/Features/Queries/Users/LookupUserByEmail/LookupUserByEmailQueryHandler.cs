using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Users.LookupUserByEmail;

public sealed class LookupUserByEmailQueryHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    IOptions<SuperAdminOptions> superAdminOptions)
    : IQueryHandler<LookupUserByEmailQuery, LookupUserByEmailResponse>
{
    public async Task<ErrorOr<LookupUserByEmailResponse>> Handle(
        LookupUserByEmailQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var isReservedEmail = superAdminOptions.Value.MatchesEmail(email);

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return new LookupUserByEmailResponse
            {
                Exists = false,
                AlreadyInWorkspace = false,
                CannotBeInvited = isReservedEmail,
            };
        }

        var membership = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == user.Id && tu.TenantId == tenantId,
            cancellationToken: cancellationToken);

        return new LookupUserByEmailResponse
        {
            Exists = true,
            AlreadyInWorkspace = membership?.Status == TenantUserStatus.Active,
            CannotBeInvited = isReservedEmail,
            FirstName = user.FirstName,
            LastName = user.LastName,
        };
    }
}
