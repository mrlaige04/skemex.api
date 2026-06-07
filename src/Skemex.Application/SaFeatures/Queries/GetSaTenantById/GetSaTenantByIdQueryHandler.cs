using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.GetSaTenantById;

public sealed class GetSaTenantByIdQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository,
    IUrlService urlService)
    : IQueryHandler<GetSaTenantByIdQuery, SaTenantDto>
{
    public async Task<ErrorOr<SaTenantDto>> Handle(
        GetSaTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
        }

        var tenant = await tenantRepository.GetAsync(
            filter: t => t.Id == request.TenantId,
            include: q => q.Include(t => t.Users),
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(SaTenantErrors.NotFound, SaTenantErrors.NotFoundDescription);
        }

        return new SaTenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Email = tenant.Email,
            CreatedAt = tenant.CreatedAt,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
            MemberCount = tenant.Users.Count,
        };
    }
}
