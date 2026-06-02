namespace Skemex.Domain.Entities.Abstractions;

public interface IEntity<TKey>
{
    TKey Id { get; set; }
}