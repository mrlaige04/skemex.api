using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Tenants.GetTenantSettings;

public sealed class GetTenantSettingsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSettings> tenantSettingsRepository,
    TenantSettingsSeeder tenantSettingsSeeder)
    : IQueryHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    public async Task<ErrorOr<TenantSettingsDto>> Handle(
        GetTenantSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing settings.");
        }

        await tenantSettingsSeeder.EnsureDefaultsAsync(tenantId.Value, cancellationToken);

        var settings = await tenantSettingsRepository.GetAsync(
            filter: entry => entry.TenantId == tenantId.Value,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("TenantSettings.NotFound", "Workspace settings were not found.");
        }

        return TenantSettingsDto.FromEntity(settings);
    }
}
