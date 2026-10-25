using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Features.Queries.Users.GetTenantRoles;

public sealed class GetTenantRolesQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Role> roleRepository,
    TenantRbacSeeder tenantRbacSeeder)
    : IQueryHandler<GetTenantRolesQuery, IReadOnlyList<TenantRoleDto>>
{
    public async Task<ErrorOr<IReadOnlyList<TenantRoleDto>>> Handle(
        GetTenantRolesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        await tenantRbacSeeder.EnsureDefaultsAsync(tenantId.Value, cancellationToken);

        var roles = await roleRepository.GetAllAsync(
            filter: r => r.TenantId == tenantId,
            cancellationToken: cancellationToken);

        return roles
            .OrderBy(r => r.IsSystem ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new TenantRoleDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                IsSystem = r.IsSystem,
            })
            .ToList();
    }
}
