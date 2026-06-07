using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.TenantUsers.Commands.UpdateTenantUser;

public sealed class UpdateTenantUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Role> roleRepository,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<UpdateTenantUserCommand, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        UpdateTenantUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantIdResult = TenantUserContext.RequireTenantId(currentUser);
        if (tenantIdResult.IsError)
        {
            return tenantIdResult.Errors;
        }

        var tenantId = tenantIdResult.Value;
        var tenantUser = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == request.UserId,
            include: q => q.Include(tu => tu.User),
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var user = tenantUser.User;
        var changed = false;

        if (request.FirstName is not null)
        {
            var t = request.FirstName.Trim();
            if (t.Length > 0 && t != user.FirstName)
            {
                user.FirstName = t;
                changed = true;
            }
        }

        if (request.LastName is not null)
        {
            var t = request.LastName.Trim();
            if (t.Length > 0 && t != user.LastName)
            {
                user.LastName = t;
                changed = true;
            }
        }

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (email.Length > 0 && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (superAdminOptions.Value.MatchesEmail(email))
                {
                    return Error.Validation(
                        TenantUserErrors.SuperAdminEmailReserved,
                        TenantUserErrors.SuperAdminEmailReservedDescription);
                }

                var taken = await userManager.FindByEmailAsync(email);
                if (taken is not null && taken.Id != user.Id)
                {
                    return Error.Conflict(
                        TenantUserErrors.EmailAlreadyExists,
                        TenantUserErrors.EmailAlreadyExistsDescription);
                }

                user.Email = email;
                user.UserName = email;
                user.NormalizedEmail = userManager.NormalizeEmail(email);
                user.NormalizedUserName = userManager.NormalizeName(email);
                changed = true;
            }
        }

        if (changed)
        {
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return Error.Validation(
                    "User.UpdateFailed",
                    string.Join(' ', update.Errors.Select(e => e.Description)));
            }
        }

        if (request.RoleName is not null)
        {
            var roleName = request.RoleName.Trim();
            var role = await roleRepository.GetAsync(
                r => r.TenantId == tenantId && r.Name == roleName,
                cancellationToken: cancellationToken);
            if (role is null)
            {
                return Error.Validation("Role.NotFound", $"Role '{roleName}' is not available in this workspace.");
            }

            var existingRoles = await userRoleRepository.GetAllAsync(
                filter: ur => ur.TenantId == tenantId && ur.UserId == user.Id,
                cancellationToken: cancellationToken);

            foreach (var ur in existingRoles)
            {
                await userRoleRepository.DeleteAsync(ur, cancellationToken);
            }

            await userRoleRepository.AddAsync(
                new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id,
                    TenantId = tenantId,
                },
                cancellationToken);
        }

        var userRoles = await userRoleRepository.GetAllAsync(
            filter: ur => ur.TenantId == tenantId && ur.UserId == user.Id,
            include: q => q.Include(ur => ur.Role),
            cancellationToken: cancellationToken);

        var avatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken).ConfigureAwait(false);

        return new TenantUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            Roles = userRoles.Select(ur => ur.Role.Name).Where(n => n is not null).Cast<string>().OrderBy(n => n).ToList(),
            Status = tenantUser.Status,
            AvatarUrl = avatarUrl,
        };
    }
}
