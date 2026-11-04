using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Users;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.CreateTenant;

public class CreateTenantHandler(
    ICurrentUser currentUser,
    TenantProvisioningService tenantProvisioning,
    IUrlService urlService)
    : ICommandHandler<CreateTenantCommand, TenantSummaryResponse>
{
    public async Task<ErrorOr<TenantSummaryResponse>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Unauthenticated", "Authentication required.");
        }

        var created = await tenantProvisioning.CreateWorkspaceAsync(
            request.Name,
            request.Email,
            userId.Value,
            uniquifyName: false,
            cancellationToken);

        if (created.IsError)
        {
            return created.Errors;
        }

        var tenant = created.Value;
        return new TenantSummaryResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
        };
    }
}
