using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Users.GetTenantRoles;

public sealed class GetTenantRolesQueryHandler(
    ICurrentUser currentUser,
    RoleManager<Role> roleManager,
    IBaseRepository<Role> roleRepository)
    : IQueryHandler<GetTenantRolesQuery, IReadOnlyList<TenantRoleDto>>
{
    private static readonly string[] AssignableRoleNames = [RoleNames.Admin, RoleNames.User];

    public async Task<ErrorOr<IReadOnlyList<TenantRoleDto>>> Handle(
        GetTenantRolesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        await EnsureAssignableRolesAsync(tenantId.Value, cancellationToken);

        var roles = await roleRepository.GetAllAsync(
            filter: r => r.TenantId == tenantId && AssignableRoleNames.Contains(r.Name!),
            cancellationToken: cancellationToken);

        return roles
            .OrderBy(r => r.Name)
            .Select(r => new TenantRoleDto { Id = r.Id, Name = r.Name ?? string.Empty })
            .ToList();
    }

    private async Task EnsureAssignableRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        foreach (var roleName in AssignableRoleNames)
        {
            var exists = await roleRepository.ExistsAsync(
                r => r.TenantId == tenantId && r.Name == roleName,
                cancellationToken: cancellationToken);
            if (exists)
            {
                continue;
            }

            await roleManager.CreateAsync(new Role
            {
                Name = roleName,
                TenantId = tenantId,
                IsSystem = true,
            });
        }
    }
}
