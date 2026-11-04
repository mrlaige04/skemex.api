using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Users;

/// <summary>
/// Creates a tenant workspace with RBAC/columns seed and Admin membership for the owner (and optional creator).
/// </summary>
public sealed class TenantProvisioningService(
    UserManager<User> userManager,
    IBaseRepository<Tenant> tenantRepository,
    IBaseRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Role> roleRepository,
    TenantColumnSeeder tenantColumnSeeder,
    TenantRbacSeeder tenantRbacSeeder)
{
    public async Task<ErrorOr<Tenant>> CreateWorkspaceAsync(
        string name,
        string email,
        Guid creatorUserId,
        bool uniquifyName = false,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return Error.Validation("Tenant.InvalidName", "Workspace name is required.");
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Error.Validation("Tenant.InvalidEmail", "Workspace email is required.");
        }

        string uniqueName;
        if (uniquifyName)
        {
            uniqueName = await EnsureUniqueTenantNameAsync(trimmedName, cancellationToken);
        }
        else if (await tenantRepository.ExistsAsync(
                     t => t.Name == trimmedName,
                     cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.NameTaken", "A company with this name already exists.");
        }
        else
        {
            uniqueName = trimmedName;
        }

        if (await tenantRepository.ExistsAsync(
                t => t.Email == normalizedEmail,
                cancellationToken: cancellationToken))
        {
            return Error.Conflict("Tenant.EmailTaken", "A company with this email already exists.");
        }

        var creator = await userManager.FindByIdAsync(creatorUserId.ToString());
        if (creator is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var owner = await userManager.FindByEmailAsync(normalizedEmail);
        if (owner is null)
        {
            owner = new User
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FirstName = ResolveFirstName(normalizedEmail, uniqueName),
                LastName = string.Empty,
                EmailConfirmed = true,
            };

            var createUser = await userManager.CreateAsync(owner);
            if (!createUser.Succeeded)
            {
                if (createUser.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    owner = await userManager.FindByEmailAsync(normalizedEmail);
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
            Name = uniqueName,
            Email = normalizedEmail,
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

        if (creator.Id != owner.Id)
        {
            await EnsureTenantAdminAsync(creator.Id, tenant.Id, adminRole.Id, cancellationToken);
        }

        await tenantColumnSeeder.EnsureDefaultsAsync(tenant.Id, cancellationToken);
        return tenant;
    }

    /// <summary>Creates <c>{First} {Last} Workspace</c> owned by the user (email = user email).</summary>
    public async Task<ErrorOr<Tenant>> CreatePersonalWorkspaceAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var display = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(display))
        {
            display = user.Email?.Split('@', 2)[0] ?? "My";
        }

        var email = user.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Error.Validation("Tenant.InvalidEmail", "User email is required to create a workspace.");
        }

        // Personal workspace reuses the user's email; if taken, suffix a unique local-part.
        var workspaceEmail = email;
        if (await tenantRepository.ExistsAsync(t => t.Email == workspaceEmail, cancellationToken: cancellationToken))
        {
            var at = email.IndexOf('@');
            var local = at > 0 ? email[..at] : email;
            var domain = at > 0 ? email[at..] : "@skemex.local";
            var suffix = Guid.NewGuid().ToString("N")[..8];
            workspaceEmail = $"{local}+ws{suffix}{domain}";
        }

        return await CreateWorkspaceAsync(
            $"{display} Workspace",
            workspaceEmail,
            user.Id,
            uniquifyName: true,
            cancellationToken);
    }

    public async Task<string> EnsureUniqueTenantNameAsync(
        string baseName,
        CancellationToken cancellationToken = default)
    {
        var candidate = baseName.Trim();
        if (candidate.Length == 0)
        {
            candidate = "Workspace";
        }

        if (!await tenantRepository.ExistsAsync(t => t.Name == candidate, cancellationToken: cancellationToken))
        {
            return candidate;
        }

        for (var i = 2; i < 1000; i++)
        {
            var next = $"{candidate} ({i})";
            if (!await tenantRepository.ExistsAsync(t => t.Name == next, cancellationToken: cancellationToken))
            {
                return next;
            }
        }

        var fallback = Guid.NewGuid().ToString("N")[..8];
        return $"{candidate} ({fallback})";
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
