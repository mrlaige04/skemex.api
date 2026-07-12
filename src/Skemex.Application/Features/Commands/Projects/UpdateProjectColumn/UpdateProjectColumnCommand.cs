using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectColumn;

public sealed class UpdateProjectColumnCommand : ICommand<ProjectColumnDto>
{
    public Guid ProjectId { get; init; }
    public Guid ColumnId { get; init; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}
