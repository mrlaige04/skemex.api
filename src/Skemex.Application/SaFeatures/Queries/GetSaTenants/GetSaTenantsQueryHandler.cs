using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.GetSaTenants;

public sealed class GetSaTenantsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository,
    IUrlService urlService)
    : IQueryHandler<GetSaTenantsQuery, PaginatedList<SaTenantDto>>
{
    public async Task<ErrorOr<PaginatedList<SaTenantDto>>> Handle(
        GetSaTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<Tenant, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = t =>
                t.Name.ToLower().Contains(term) ||
                t.Email.ToLower().Contains(term);
        }

        var paginated = await tenantRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q
                .Include(t => t.Users)
                .OrderBy(t => t.Name),
            cancellationToken: cancellationToken);

        var items = paginated.Items
            .Select(t => ToDto(t, urlService))
            .ToList();

        return new PaginatedList<SaTenantDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }

    private static SaTenantDto ToDto(Tenant tenant, IUrlService urlService) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Email = tenant.Email,
            CreatedAt = tenant.CreatedAt,
            LogoUrl = urlService.GetTenantLogoUrl(tenant.LogoBlobId),
            MemberCount = tenant.Users.Count,
        };
}
