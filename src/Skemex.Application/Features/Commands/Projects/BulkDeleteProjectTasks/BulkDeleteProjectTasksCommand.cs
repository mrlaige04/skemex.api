using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.BulkDeleteProjectTasks;

public sealed class BulkDeleteProjectTasksCommand : ICommand
{
    public Guid ProjectId { get; init; }

    public IReadOnlyList<Guid> TaskIds { get; init; } = [];
}
