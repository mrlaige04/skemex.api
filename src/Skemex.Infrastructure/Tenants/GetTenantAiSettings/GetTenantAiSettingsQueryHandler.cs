using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Tenants.GetTenantAiSettings;

public sealed class GetTenantAiSettingsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository)
    : IQueryHandler<GetTenantAiSettingsQuery, TenantAiSettingsDto>
{
    public async Task<ErrorOr<TenantAiSettingsDto>> Handle(
        GetTenantAiSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing AI settings.");
        }

        if (!currentUser.HasPermission(PermissionNames.SettingsWorkspaceView))
        {
            return Error.Forbidden("Tenant.AiSettingsDenied", "You do not have permission to view AI settings.");
        }

        var tenant = await tenantRepository.GetAsync(
            t => t.Id == tenantId.Value,
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        return new TenantAiSettingsDto
        {
            AiTemperature = Math.Clamp(
                tenant.AiTemperature,
                AiSamplingDefaults.MinTemperature,
                AiSamplingDefaults.MaxTemperature),
        };
    }
}
