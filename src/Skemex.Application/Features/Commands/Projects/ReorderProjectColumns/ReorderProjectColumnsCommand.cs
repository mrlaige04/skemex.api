using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.ReorderProjectColumns;

public sealed class ReorderProjectColumnsCommand : ICommand<IReadOnlyList<ProjectColumnDto>>
{
    public Guid ProjectId { get; init; }
    public IReadOnlyList<Guid> ColumnIds { get; init; } = [];
}
