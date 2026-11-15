using Microsoft.Extensions.Logging;
using Skemex.Application.Events.Abstractions;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Events.Projects;

namespace Skemex.Application.Events.Projects;

public sealed class ProjectBoardChangedRealtimeHandler(
    IProjectBoardRealtimePublisher publisher,
    ILogger<ProjectBoardChangedRealtimeHandler> logger)
    : IDomainEventHandler<ProjectBoardChangedDomainEvent>
{
    public async Task Handle(
        ProjectBoardChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await publisher.PublishBoardChangedAsync(
                domainEvent.ProjectId,
                domainEvent.ChangeKind,
                domainEvent.TaskId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to publish board change for project {ProjectId} ({ChangeKind}).",
                domainEvent.ProjectId,
                domainEvent.ChangeKind);
        }
    }
}
