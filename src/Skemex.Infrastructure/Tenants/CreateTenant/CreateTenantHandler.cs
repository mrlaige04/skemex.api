using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Users;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.CreateTenant;

public class CreateTenantHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IBaseRepository<Tenant> tenantRepository,
    IBaseRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Role> roleRepository,
    IUrlService urlService,
    TenantColumnSeeder tenantColumnSeeder,
    TenantRbacSeeder tenantRbacSeeder)
    : ICommandHandler<CreateTenantCommand, TenantSummaryResponse>
{
    public async Task<ErrorOr<TenantSummaryResponse>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Unauthenticated", "Authentication required.");
        }

        var creator = await userManager.FindByIdAsync(userId.Value.ToString());
        if (creator?.Email is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await tenantRepository.ExistsAsync(t => t.Name == name, cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.NameTaken", "A company with this name already exists.");
        }

        if (await tenantRepository.ExistsAsync(t => t.Email == email, cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.EmailTaken", "A company with this email already exists.");
        }

        // Owner is the company email account (create if needed), matching SA tenant create.
        var owner = await userManager.FindByEmailAsync(email);
        if (owner is null)
        {
            owner = new User
            {
                UserName = email,
                Email = email,
                FirstName = ResolveFirstName(email, name),
                LastName = string.Empty,
                EmailConfirmed = true,
            };

            var createUser = await userManager.CreateAsync(owner);
            if (!createUser.Succeeded)
            {
                if (createUser.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    owner = await userManager.FindByEmailAsync(email);
                    if (owner is null)
                    {
                        return Error.Failure(
                            "Tenant.UserCreateFailed",
                            string.Join(' ', createUser.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    return Error.Validation(
                        "Tenant.UserCreateFailed",
                        string.Join(' ', createUser.Errors.Select(e => e.Description)));
                }
            }
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await tenantRbacSeeder.SeedForNewTenantAsync(tenant.Id, cancellationToken);

        var adminRole = await roleRepository.GetAsync(
            r => r.TenantId == tenant.Id && r.Name == RoleNames.Admin,
            cancellationToken: cancellationToken);
        if (adminRole is null)
        {
            return Error.Failure("Tenant.RoleCreateFailed", "Failed to seed Admin role for the workspace.");
        }

        await EnsureTenantAdminAsync(owner.Id, tenant.Id, adminRole.Id, cancellationToken);

        // Creator always gets Admin so they can enter the workspace they just created.
        if (creator.Id != owner.Id)
        {
            await EnsureTenantAdminAsync(creator.Id, tenant.Id, adminRole.Id, cancellationToken);
        }

        await tenantColumnSeeder.EnsureDefaultsAsync(tenant.Id, cancellationToken);

        return new TenantSummaryResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
        };
    }

    private async Task EnsureTenantAdminAsync(
        Guid userId,
        Guid tenantId,
        Guid adminRoleId,
        CancellationToken cancellationToken)
    {
        var alreadyLinked = await tenantUserRepository.ExistsAsync(
            tu => tu.UserId == userId && tu.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (!alreadyLinked)
        {
            await tenantUserRepository.AddAsync(
                new TenantUser
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TenantId = tenantId,
                    Status = TenantUserStatus.Active,
                },
                cancellationToken);
        }

        var hasAdmin = await userRoleRepository.ExistsAsync(
            ur => ur.UserId == userId && ur.TenantId == tenantId && ur.RoleId == adminRoleId,
            cancellationToken: cancellationToken);

        if (!hasAdmin)
        {
            await userRoleRepository.AddAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = adminRoleId,
                    TenantId = tenantId,
                },
                cancellationToken);
        }
    }

    private static string ResolveFirstName(string email, string tenantName)
    {
        var localPart = email.Split('@', 2)[0].Trim();
        if (!string.IsNullOrEmpty(localPart))
        {
            return char.ToUpperInvariant(localPart[0]) + localPart[1..];
        }

        return tenantName;
    }
}
