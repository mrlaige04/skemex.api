namespace Skemex.Domain.Entities.Abstractions;

public interface ITenantEntity<TKey> : IEntity<TKey>
{
    TKey TenantId { get; set; }
}