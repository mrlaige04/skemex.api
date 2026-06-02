using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Domain.Repositories;

public class TenantBaseRepository<TTenantEntity>(
    DbContext dbContext,
    ICurrentUser currentUser) 
    : BaseRepository<TTenantEntity>(dbContext), ITenantRepository<TTenantEntity>
    where TTenantEntity : class, ITenantEntity<Guid>
{
    public override async Task<IQueryable<TTenantEntity>> GetQuery(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetTenantId();
        var baseQuery = await base.GetQuery(cancellationToken);
        
        return tenantId.HasValue ? baseQuery.Where(t => t.TenantId == tenantId) : baseQuery;
    }
}