using Microsoft.Extensions.DependencyInjection;
using Skemex.Domain.Events.Abstractions;

namespace Skemex.Application.Events.Abstractions;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                var handleMethod = handlerType.GetMethod(
                    nameof(IDomainEventHandler<IDomainEvent>.Handle),
                    [domainEvent.GetType(), typeof(CancellationToken)]);

                if (handleMethod is null)
                {
                    continue;
                }

                var task = (Task?)handleMethod.Invoke(
                    handler,
                    [domainEvent, cancellationToken]);
                if (task is not null)
                {
                    await task.ConfigureAwait(false);
                }
            }
        }
    }
}
