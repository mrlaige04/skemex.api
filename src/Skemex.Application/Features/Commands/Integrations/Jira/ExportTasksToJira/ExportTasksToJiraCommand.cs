using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.ExportTasksToJira;

public sealed class ExportTasksToJiraCommand : ICommand<ExportTasksToJiraResultDto>
{
    public Guid ProjectId { get; init; }
    public JiraExportScope Scope { get; init; } = JiraExportScope.AllFiltered;
    public IReadOnlyList<Guid> SelectedTaskIds { get; init; } = [];
    public string? Search { get; init; }
    public Guid? ColumnId { get; init; }
    public Guid? AssigneeId { get; init; }
    public bool Unassigned { get; init; }
    public bool SkipAlreadyExported { get; init; } = true;
}
