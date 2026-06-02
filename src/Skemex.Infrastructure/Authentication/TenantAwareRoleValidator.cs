using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Data;

namespace Skemex.Infrastructure.Authentication;

/// <summary>
/// Replaces the default <see cref="RoleValidator{TRole}"/>, which treats role names as globally unique.
/// Tenant roles are unique per <see cref="Role.TenantId"/>; global roles use <c>TenantId == null</c>.
/// </summary>
public sealed class TenantAwareRoleValidator(SkemexDbContext db, IdentityErrorDescriber errors) : IRoleValidator<Role>
{
    public async Task<IdentityResult> ValidateAsync(RoleManager<Role> manager, Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrWhiteSpace(role.Name))
        {
            return IdentityResult.Failed(errors.InvalidRoleName(role.Name ?? string.Empty));
        }

        var normalized = role.NormalizedName;
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = manager.KeyNormalizer?.NormalizeName(role.Name) ?? role.Name.ToUpperInvariant();
        }

        if (string.IsNullOrEmpty(normalized))
        {
            return IdentityResult.Failed(errors.InvalidRoleName(role.Name));
        }

        var query = db.Roles.AsNoTracking().Where(r => r.NormalizedName == normalized);

        if (role.TenantId is { } tid)
        {
            query = query.Where(r => r.TenantId == tid);
        }
        else
        {
            query = query.Where(r => r.TenantId == null);
        }

        if (role.Id != default)
        {
            query = query.Where(r => r.Id != role.Id);
        }

        if (await query.AnyAsync(CancellationToken.None).ConfigureAwait(false))
        {
            return IdentityResult.Failed(errors.DuplicateRoleName(role.Name));
        }

        return IdentityResult.Success;
    }
}
