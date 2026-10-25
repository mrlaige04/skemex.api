using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Skemex.Application.Events.Abstractions;
using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Events.Abstractions;

namespace Skemex.Infrastructure.Data.Interceptors;

/// <summary>Dispatches domain events after a successful SaveChanges.</summary>
public sealed class PublishDomainEventsInterceptor(IServiceScopeFactory scopeFactory) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchEvents(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchEvents(DbContext? context, CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        var domainEvents = new List<IDomainEvent>();
        foreach (var entity in entities)
        {
            domainEvents.AddRange(entity.DomainEvents);
            entity.ClearEvents();
        }

        // New scope so handlers can safely use DbContext without re-entering this interceptor's call stack oddly.
        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
    }
}
