using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectColumn;

public sealed class DeleteProjectColumnCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid ColumnId { get; init; }
}
