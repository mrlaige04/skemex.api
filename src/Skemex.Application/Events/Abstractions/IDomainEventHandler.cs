using Skemex.Domain.Events.Abstractions;

namespace Skemex.Application.Events.Abstractions;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken = default);
}
