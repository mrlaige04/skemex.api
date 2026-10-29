using System.Linq.Expressions;
using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.ExportProjectTasks;

public sealed class ExportProjectTasksQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IProjectTasksExportService exportService)
    : IQueryHandler<ExportProjectTasksQuery, ExportFileResult>
{
    public const int MaxExportRows = 10_000;

    public async Task<ErrorOr<ExportFileResult>> Handle(
        ExportProjectTasksQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var project = await projectRepository.GetAsync(
            filter: p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        Expression<Func<ProjectTask, bool>> filter;
        if (request.Scope == ExportTasksScope.Selected)
        {
            var ids = request.SelectedTaskIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return Error.Validation(
                    "ProjectTask.IdsRequired",
                    "Select at least one issue to export.");
            }

            if (ids.Count > MaxExportRows)
            {
                return Error.Validation(
                    "ProjectTask.ExportTooMany",
                    $"You can export at most {MaxExportRows} issues at once.");
            }

            filter = ProjectTaskQueryHelper.BuildSelectedFilter(request.ProjectId, ids);
        }
        else
        {
            filter = ProjectTaskQueryHelper.BuildFilter(
                request.ProjectId,
                request.ColumnId,
                request.AssigneeId,
                request.Unassigned,
                request.Search);
        }

        var matchingCount = await projectTaskRepository.CountAsync(
            filter: filter,
            cancellationToken: cancellationToken);

        if (matchingCount == 0)
        {
            return Error.Validation(
                "ProjectTask.ExportEmpty",
                "No issues match the export criteria.");
        }

        if (matchingCount > MaxExportRows)
        {
            return Error.Validation(
                "ProjectTask.ExportTooMany",
                $"Export is limited to {MaxExportRows} issues. Narrow your filters or export a selection.");
        }

        var tasks = await projectTaskRepository.GetAllAsync(
            filter: filter,
            include: query => ProjectTaskQueryHelper.ApplySort(
                ProjectTaskQueryHelper.IncludeExportRelations(query),
                request.Sort),
            cancellationToken: cancellationToken);

        var rows = tasks.Select(MapRow).ToList();
        return exportService.Generate(rows, request.Format, project.Code);
    }

    private static ExportTaskRow MapRow(ProjectTask task)
    {
        static string PersonName(Domain.Entities.Users.User? user)
        {
            if (user is null)
            {
                return string.Empty;
            }

            var full = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrEmpty(full) ? user.Email : full;
        }

        return new ExportTaskRow
        {
            Code = task.Code,
            Title = task.Title,
            Type = task.Type.ToString(),
            Status = task.Column?.Title ?? string.Empty,
            ParentTask = task.Parent?.Code ?? string.Empty,
            Description = task.Description,
            Assignee = PersonName(task.Assignee),
            Reporter = PersonName(task.Reporter),
            StoryPoints = task.StoryPoints,
            OriginalEstimateMinutes = task.OriginalEstimateMinutes,
            RemainingEstimateMinutes = task.RemainingEstimateMinutes,
            SpentMinutes = task.SpentMinutes,
            Tags = task.Tags is { Count: > 0 } ? string.Join(", ", task.Tags) : string.Empty,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };
    }
}
