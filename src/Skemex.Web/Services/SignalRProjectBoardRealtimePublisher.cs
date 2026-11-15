using Microsoft.AspNetCore.SignalR;
using Skemex.Application.Services.Projects;
using Skemex.Web.Hubs;

namespace Skemex.Web.Services;

public sealed class SignalRProjectBoardRealtimePublisher(
    IHubContext<ProjectBoardHub> hubContext) : IProjectBoardRealtimePublisher
{
    public Task PublishBoardChangedAsync(
        Guid projectId,
        string changeKind,
        Guid? taskId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            projectId,
            changeKind,
            taskId,
        };

        return hubContext.Clients
            .Group(ProjectBoardHub.GroupName(projectId))
            .SendAsync(ProjectBoardHub.BoardChangedMethod, payload, cancellationToken);
    }
}
