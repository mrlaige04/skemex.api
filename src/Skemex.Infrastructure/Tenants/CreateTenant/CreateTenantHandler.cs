using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.CreateTenant;

public class CreateTenantHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IBaseRepository<Tenant> tenantRepository,
    IBaseRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IUrlService urlService)
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

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user?.Email is null)
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

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);

        var adminRole = new Role
        {
            Name = RoleNames.Admin,
            TenantId = tenant.Id,
            IsSystem = true,
        };

        var roleCreate = await roleManager.CreateAsync(adminRole);
        if (!roleCreate.Succeeded)
        {
            return Error.Validation(
                "Tenant.RoleCreateFailed",
                string.Join(' ', roleCreate.Errors.Select(e => e.Description)));
        }

        var userRoleEntity = new Role
        {
            Name = RoleNames.User,
            TenantId = tenant.Id,
            IsSystem = true,
        };

        var userRoleCreate = await roleManager.CreateAsync(userRoleEntity);
        if (!userRoleCreate.Succeeded)
        {
            return Error.Validation(
                "Tenant.RoleCreateFailed",
                string.Join(' ', userRoleCreate.Errors.Select(e => e.Description)));
        }

        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            TenantId = tenant.Id,
        };
        await tenantUserRepository.AddAsync(tenantUser, cancellationToken);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            RoleId = adminRole.Id,
            TenantId = tenant.Id,
        };
        await userRoleRepository.AddAsync(userRole, cancellationToken);

        return new TenantSummaryResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
        };
    }
}
