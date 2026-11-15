using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Tenants.UpdateTenantAiSettings;

public sealed class UpdateTenantAiSettingsCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository)
    : ICommandHandler<UpdateTenantAiSettingsCommand, TenantAiSettingsDto>
{
    public async Task<ErrorOr<TenantAiSettingsDto>> Handle(
        UpdateTenantAiSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before updating AI settings.");
        }

        if (!currentUser.HasPermission(PermissionNames.SettingsWorkspaceEdit))
        {
            return Error.Forbidden("Tenant.AiSettingsDenied", "You do not have permission to edit AI settings.");
        }

        var tenant = await tenantRepository.GetAsync(
            t => t.Id == tenantId.Value,
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        tenant.AiTemperature = Math.Clamp(
            request.AiTemperature,
            AiSamplingDefaults.MinTemperature,
            AiSamplingDefaults.MaxTemperature);

        await tenantRepository.UpdateAsync(tenant, cancellationToken);

        return new TenantAiSettingsDto { AiTemperature = tenant.AiTemperature };
    }
}