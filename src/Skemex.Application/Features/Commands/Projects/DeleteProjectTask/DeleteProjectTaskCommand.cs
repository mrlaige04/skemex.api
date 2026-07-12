using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTask;

public sealed class DeleteProjectTaskCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid ColumnId { get; init; }
    public Guid TaskId { get; init; }
}
