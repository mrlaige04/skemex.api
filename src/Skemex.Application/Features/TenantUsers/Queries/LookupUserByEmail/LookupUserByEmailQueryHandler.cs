using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.TenantUsers.Queries.LookupUserByEmail;

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
        var tenantIdResult = TenantUserContext.RequireTenantId(currentUser);
        if (tenantIdResult.IsError)
        {
            return tenantIdResult.Errors;
        }

        var tenantId = tenantIdResult.Value;
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
