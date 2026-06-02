using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Repositories.Abstractions;

public interface ITenantRepository<TTenantEntity> : IBaseRepository<TTenantEntity> 
    where TTenantEntity : class, ITenantEntity<Guid>
{
    
}