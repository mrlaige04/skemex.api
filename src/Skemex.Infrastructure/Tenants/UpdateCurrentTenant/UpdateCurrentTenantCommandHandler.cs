using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Common;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.UpdateCurrentTenant;

public sealed class UpdateCurrentTenantCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository,
    IUrlService urlService)
    : ICommandHandler<UpdateCurrentTenantCommand, TenantSummaryResponse>
{
    public async Task<ErrorOr<TenantSummaryResponse>> Handle(
        UpdateCurrentTenantCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before updating it.");
        }

        if (!currentUser.HasPermission(PermissionNames.SettingsWorkspaceEdit))
        {
            return Error.Forbidden("Tenant.UpdateDenied", "You do not have permission to edit this workspace.");
        }

        var tenant = await tenantRepository.GetAsync(
            t => t.Id == tenantId.Value,
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        var name = request.Name.Trim();
        if (!string.Equals(name, tenant.Name, StringComparison.Ordinal))
        {
            if (await tenantRepository.ExistsAsync(
                    t => t.Name == name && t.Id != tenant.Id,
                    cancellationToken: cancellationToken))
            {
                return Error.Conflict("Tenant.NameTaken", "A company with this name already exists.");
            }

            tenant.Name = name;
            await tenantRepository.UpdateAsync(tenant, cancellationToken);
        }

        return new TenantSummaryResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
        };
    }
}
