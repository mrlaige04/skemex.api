using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTaskWorkLog;

public sealed class DeleteProjectTaskWorkLogCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Guid WorkLogId { get; init; }
}
