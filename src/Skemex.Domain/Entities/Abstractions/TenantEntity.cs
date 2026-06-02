namespace Skemex.Domain.Entities.Abstractions;

public abstract class TenantEntity : BaseEntity, ITenantEntity<Guid>
{
    public Guid TenantId { get; set; }
}