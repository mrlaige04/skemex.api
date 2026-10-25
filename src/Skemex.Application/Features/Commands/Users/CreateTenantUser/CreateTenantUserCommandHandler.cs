using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Events.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Helpers.Users;

namespace Skemex.Application.Features.Commands.Users.CreateTenantUser;

public sealed class CreateTenantUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<Tenant> tenantRepository,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<CreateTenantUserCommand, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        CreateTenantUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.RoleName.Trim();

        if (superAdminOptions.Value.MatchesEmail(email))
        {
            return Error.Validation(
                "User.SuperAdminEmailReserved",
                "This email address is reserved and cannot be used.");
        }

        var tenant = await tenantRepository.GetAsync(t => t.Id == tenantId, cancellationToken: cancellationToken);
        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        var role = await roleRepository.GetAsync(
            r => r.TenantId == tenantId && r.Name == roleName,
            cancellationToken: cancellationToken);
        if (role is null)
        {
            return Error.Validation("Role.NotFound", $"Role '{roleName}' is not available in this workspace.");
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        User user;
        var isNewUser = existingUser is null;

        if (isNewUser)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                EmailConfirmed = true,
            };

            var create = await userManager.CreateAsync(user);
            if (!create.Succeeded)
            {
                if (create.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    return Error.Conflict(
                        "User.EmailAlreadyExists",
                        "An account with this email already exists.");
                }

                return Error.Validation(
                    "User.RegistrationFailed",
                    string.Join(' ', create.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            user = existingUser!;
        }

        var existingMembership = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == user.Id && tu.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (existingMembership is not null)
        {
            if (existingMembership.Status == TenantUserStatus.Active)
            {
                return Error.Conflict("User.AlreadyInTenant", "This user is already linked to the workspace.");
            }

            return await ResendInvitationAsync(
                existingMembership,
                user,
                tenant,
                role,
                cancellationToken);
        }

        await RemoveUserRolesInTenantAsync(user.Id, tenantId.Value, cancellationToken);

        var invitationToken = InvitationTokenHelper.Create();
        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId.Value,
            Status = TenantUserStatus.Pending,
            InvitationToken = invitationToken,
            InvitationTokenExpiresAt = InvitationTokenHelper.ExpiresAt(),
        };
        RaiseInvited(tenantUser, tenant.Name, invitationToken);
        await tenantUserRepository.AddAsync(tenantUser, cancellationToken);

        await AssignRoleAsync(user.Id, role, tenantId.Value, cancellationToken);

        var avatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken).ConfigureAwait(false);

        return new TenantUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            Roles = [roleName],
            Status = TenantUserStatus.Pending,
            AvatarUrl = avatarUrl,
        };
    }

    private async Task<ErrorOr<TenantUserDto>> ResendInvitationAsync(
        TenantUser tenantUser,
        User user,
        Tenant tenant,
        Role role,
        CancellationToken cancellationToken)
    {
        var invitationToken = InvitationTokenHelper.Create();
        tenantUser.Status = TenantUserStatus.Pending;
        tenantUser.InvitationToken = invitationToken;
        tenantUser.InvitationTokenExpiresAt = InvitationTokenHelper.ExpiresAt();
        RaiseInvited(tenantUser, tenant.Name, invitationToken);
        await tenantUserRepository.UpdateAsync(tenantUser, cancellationToken);

        await RemoveUserRolesInTenantAsync(user.Id, tenant.Id, cancellationToken);
        await AssignRoleAsync(user.Id, role, tenant.Id, cancellationToken);

        var avatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken).ConfigureAwait(false);

        return new TenantUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            Roles = [role.Name!],
            Status = TenantUserStatus.Pending,
            AvatarUrl = avatarUrl,
        };
    }

    private static void RaiseInvited(TenantUser tenantUser, string tenantName, string invitationToken)
    {
        tenantUser.RaiseEvent(
            new TenantUserInvitedDomainEvent
            {
                TenantUserId = tenantUser.Id,
                UserId = tenantUser.UserId,
                TenantId = tenantUser.TenantId,
                TenantName = tenantName,
                InvitationToken = invitationToken,
            });
    }

    private async Task RemoveUserRolesInTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var userRoles = await userRoleRepository.GetAllAsync(
            filter: ur => ur.TenantId == tenantId && ur.UserId == userId,
            cancellationToken: cancellationToken);

        if (userRoles.Count > 0)
        {
            await userRoleRepository.DeleteRangeAsync(userRoles, cancellationToken);
        }
    }

    private async Task AssignRoleAsync(
        Guid userId,
        Role role,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = role.Id,
            TenantId = tenantId,
        };
        await userRoleRepository.AddAsync(userRole, cancellationToken);
    }
}
