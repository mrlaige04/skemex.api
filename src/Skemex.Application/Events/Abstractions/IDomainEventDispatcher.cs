using Skemex.Domain.Events.Abstractions;

namespace Skemex.Application.Events.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
