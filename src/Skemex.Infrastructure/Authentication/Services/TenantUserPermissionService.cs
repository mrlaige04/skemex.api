using Microsoft.EntityFrameworkCore;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Authentication.Services;

public sealed class TenantUserPermissionService(
    IBaseRepository<UserRole> userRoleRepository) : ITenantUserPermissionService
{
    public async Task<IReadOnlySet<string>> GetPermissionNamesAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await userRoleRepository.GetAllAsync(
            ur => ur.UserId == userId && ur.TenantId == tenantId,
            include: q => q
                .Include(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
                .ThenInclude(rp => rp.Permission),
            cancellationToken: cancellationToken);

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ur in assignments)
        {
            foreach (var rp in ur.Role.Permissions)
            {
                if (!string.IsNullOrWhiteSpace(rp.Permission?.Name))
                {
                    names.Add(rp.Permission.Name);
                }
            }
        }

        return names;
    }
}
