namespace Skemex.Application.Services.Projects;

/// <summary>Pushes live board change notifications to connected clients (e.g. SignalR).</summary>
public interface IProjectBoardRealtimePublisher
{
    Task PublishBoardChangedAsync(
        Guid projectId,
        string changeKind,
        Guid? taskId = null,
        CancellationToken cancellationToken = default);
}
