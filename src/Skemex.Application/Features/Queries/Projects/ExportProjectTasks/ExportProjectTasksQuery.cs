using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.ExportProjectTasks;

public sealed class ExportProjectTasksQuery : IQuery<ExportFileResult>
{
    public Guid ProjectId { get; init; }
    public ExportTasksScope Scope { get; init; } = ExportTasksScope.AllFiltered;
    public ExportTasksFormat Format { get; init; } = ExportTasksFormat.Xlsx;
    public IReadOnlyList<Guid> SelectedTaskIds { get; init; } = [];
    public string? Search { get; init; }
    public Guid? ColumnId { get; init; }
    public Guid? AssigneeId { get; init; }
    public bool Unassigned { get; init; }
    public string Sort { get; init; } = GetProjectTasks.ProjectTaskSort.CreatedAtDesc;
}
