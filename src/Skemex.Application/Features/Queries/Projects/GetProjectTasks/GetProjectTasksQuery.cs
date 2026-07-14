using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Abstractions;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTasks;

public sealed class GetProjectTasksQuery : IQuery<PaginatedList<ProjectTaskDto>>
{
    public Guid ProjectId { get; init; }
    public string? Search { get; init; }
    public Guid? ColumnId { get; init; }
    public Guid? AssigneeId { get; init; }
    public bool Unassigned { get; init; }
    public string Sort { get; init; } = ProjectTaskSort.CreatedAtDesc;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public static class ProjectTaskSort
{
    public const string CreatedAtDesc = "createdAtDesc";
    public const string CreatedAtAsc = "createdAtAsc";
    public const string TitleAsc = "titleAsc";
    public const string TitleDesc = "titleDesc";
    public const string CodeAsc = "codeAsc";
    public const string CodeDesc = "codeDesc";
}
