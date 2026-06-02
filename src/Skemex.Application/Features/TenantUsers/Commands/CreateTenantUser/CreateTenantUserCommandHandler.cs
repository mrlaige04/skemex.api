using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
namespace Skemex.Application.Features.TenantUsers.Commands.CreateTenantUser;

public sealed class CreateTenantUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Role> roleRepository)
    : ICommandHandler<CreateTenantUserCommand, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        CreateTenantUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantIdResult = TenantUserContext.RequireTenantId(currentUser);
        if (tenantIdResult.IsError)
        {
            return tenantIdResult.Errors;
        }

        var tenantId = tenantIdResult.Value;
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.RoleName.Trim();

        var role = await roleRepository.GetAsync(
            r => r.TenantId == tenantId && r.Name == roleName,
            cancellationToken: cancellationToken);
        if (role is null)
        {
            return Error.Validation("Role.NotFound", $"Role '{roleName}' is not available in this workspace.");
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        User user;

        if (existingUser is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                EmailConfirmed = true,
            };

            var create = await userManager.CreateAsync(user, request.Password);
            if (!create.Succeeded)
            {
                if (create.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    return Error.Conflict(
                        TenantUserErrors.EmailAlreadyExists,
                        TenantUserErrors.EmailAlreadyExistsDescription);
                }

                return Error.Validation(
                    TenantUserErrors.RegistrationFailed,
                    string.Join(' ', create.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            user = existingUser;
            if (await tenantUserRepository.ExistsAsync(tu => tu.UserId == user.Id, cancellationToken: cancellationToken))
            {
                return Error.Conflict("User.AlreadyInTenant", "This user is already a member of the workspace.");
            }
        }

        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
        };
        await tenantUserRepository.AddAsync(tenantUser, cancellationToken);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = role.Id,
            TenantId = tenantId,
        };
        await userRoleRepository.AddAsync(userRole, cancellationToken);

        return new TenantUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            Roles = [roleName],
        };
    }
}
