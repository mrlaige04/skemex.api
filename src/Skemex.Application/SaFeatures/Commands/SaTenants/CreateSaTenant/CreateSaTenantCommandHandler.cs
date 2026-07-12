using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Services;
using Microsoft.Extensions.Options;
using Skemex.Application.SaModels.SaTenants;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.CreateSaTenant;

public sealed class CreateSaTenantCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IBaseRepository<Tenant> tenantRepository,
    IBaseRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IUrlService urlService,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<CreateSaTenantCommand, SaTenantDto>
{
    public async Task<ErrorOr<SaTenantDto>> Handle(
        CreateSaTenantCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (superAdminOptions.Value.MatchesEmail(email))
        {
            return Error.Validation(
                "User.SuperAdminEmailReserved",
                "This email address is reserved and cannot be used.");
        }

        if (await tenantRepository.ExistsAsync(t => t.Name == name, cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.NameTaken", "A company with this name already exists.");
        }

        if (await tenantRepository.ExistsAsync(t => t.Email == email, cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.EmailTaken", "A company with this email already exists.");
        }

        var owner = await userManager.FindByEmailAsync(email);
        if (owner is null)
        {
            owner = new User
            {
                UserName = email,
                Email = email,
                FirstName = ResolveFirstName(request.FirstName, email, name),
                LastName = request.LastName?.Trim() ?? string.Empty,
                EmailConfirmed = true,
            };

            var createUser = await userManager.CreateAsync(owner);
            if (!createUser.Succeeded)
            {
                if (createUser.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    return Error.Conflict(
                        "User.EmailAlreadyExists",
                        "An account with this email already exists.");
                }

                return Error.Validation(
                    "Tenant.UserCreateFailed",
                    string.Join(' ', createUser.Errors.Select(e => e.Description)));
            }
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

        var adminRoleCreate = await roleManager.CreateAsync(adminRole);
        if (!adminRoleCreate.Succeeded)
        {
            return Error.Validation(
                "Tenant.RoleCreateFailed",
                string.Join(' ', adminRoleCreate.Errors.Select(e => e.Description)));
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

        var alreadyLinked = await tenantUserRepository.ExistsAsync(
            tu => tu.UserId == owner.Id && tu.TenantId == tenant.Id,
            cancellationToken: cancellationToken);

        if (!alreadyLinked)
        {
            var tenantUser = new TenantUser
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                TenantId = tenant.Id,
                Status = TenantUserStatus.Active,
            };
            await tenantUserRepository.AddAsync(tenantUser, cancellationToken);

            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                RoleId = adminRole.Id,
                TenantId = tenant.Id,
            };
            await userRoleRepository.AddAsync(userRole, cancellationToken);
        }

        return new SaTenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Email = tenant.Email,
            CreatedAt = tenant.CreatedAt,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
            MemberCount = 1,
        };
    }

    private static string ResolveFirstName(string? requested, string email, string tenantName)
    {
        var trimmed = requested?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            return trimmed;
        }

        var localPart = email.Split('@', 2)[0].Trim();
        if (!string.IsNullOrEmpty(localPart))
        {
            return localPart;
        }

        return tenantName;
    }
}
