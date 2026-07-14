namespace Skemex.Application.Models.Projects;

public sealed class ProjectTaskUserDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}

public sealed class ProjectTaskDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectColumnId { get; init; }
    public string ColumnKey { get; init; } = string.Empty;
    public string ColumnTitle { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ProjectTaskUserDto? Assignee { get; init; }
    public ProjectTaskUserDto Reporter { get; init; } = null!;
    public IReadOnlyList<ProjectTaskDto> Subtasks { get; init; } = [];
}
