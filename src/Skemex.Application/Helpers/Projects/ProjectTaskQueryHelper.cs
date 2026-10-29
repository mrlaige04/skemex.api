using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Queries.Projects.GetProjectTasks;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Helpers.Projects;

public static class ProjectTaskQueryHelper
{
    public static Expression<Func<ProjectTask, bool>> BuildFilter(
        Guid projectId,
        Guid? columnId,
        Guid? assigneeId,
        bool unassigned,
        string? search)
    {
        var term = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToLowerInvariant();

        return task =>
            task.ProjectId == projectId
            && (columnId == null || task.ProjectColumnId == columnId.Value)
            && (!unassigned || task.AssigneeId == null)
            && (unassigned || assigneeId == null || task.AssigneeId == assigneeId.Value)
            && (term == null
                || task.Code.ToLower().Contains(term)
                || task.Title.ToLower().Contains(term)
                || (task.Description != null && task.Description.ToLower().Contains(term)));
    }

    public static Expression<Func<ProjectTask, bool>> BuildSelectedFilter(
        Guid projectId,
        IReadOnlyCollection<Guid> selectedTaskIds)
    {
        return task => task.ProjectId == projectId && selectedTaskIds.Contains(task.Id);
    }

    public static IQueryable<ProjectTask> ApplySort(IQueryable<ProjectTask> query, string? sort)
    {
        return (sort ?? ProjectTaskSort.CreatedAtDesc) switch
        {
            ProjectTaskSort.CreatedAtAsc => query
                .OrderBy(task => task.CreatedAt)
                .ThenBy(task => task.Code),
            ProjectTaskSort.TitleAsc => query
                .OrderBy(task => task.Title)
                .ThenBy(task => task.Code),
            ProjectTaskSort.TitleDesc => query
                .OrderByDescending(task => task.Title)
                .ThenBy(task => task.Code),
            ProjectTaskSort.CodeAsc => query.OrderBy(task => task.Code),
            ProjectTaskSort.CodeDesc => query.OrderByDescending(task => task.Code),
            _ => query
                .OrderByDescending(task => task.CreatedAt)
                .ThenBy(task => task.Code),
        };
    }

    public static IQueryable<ProjectTask> IncludeListRelations(IQueryable<ProjectTask> query) =>
        query
            .Include(task => task.Assignee)
            .Include(task => task.Reporter)
            .Include(task => task.Column);

    public static IQueryable<ProjectTask> IncludeExportRelations(IQueryable<ProjectTask> query) =>
        IncludeListRelations(query).Include(task => task.Parent);
}
