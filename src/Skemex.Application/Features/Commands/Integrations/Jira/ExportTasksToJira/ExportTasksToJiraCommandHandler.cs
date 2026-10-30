using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.ExportTasksToJira;

public sealed class ExportTasksToJiraCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraTaskExportService exportService)
    : ICommandHandler<ExportTasksToJiraCommand, ExportTasksToJiraResultDto>
{
    public async Task<ErrorOr<ExportTasksToJiraResultDto>> Handle(
        ExportTasksToJiraCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before exporting to Jira.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (request.Scope == JiraExportScope.Selected
            && (request.SelectedTaskIds is null || request.SelectedTaskIds.Count == 0))
        {
            return Error.Validation(
                "Jira.SelectionRequired",
                "Select at least one task to export.");
        }

        return await exportService.ExportAsync(
            request.ProjectId,
            new ExportTasksToJiraRequest
            {
                Scope = request.Scope,
                SelectedTaskIds = request.SelectedTaskIds?.ToList(),
                Search = request.Search,
                ColumnId = request.ColumnId,
                AssigneeId = request.AssigneeId,
                Unassigned = request.Unassigned,
                SkipAlreadyExported = request.SkipAlreadyExported,
            },
            cancellationToken);
    }
}
