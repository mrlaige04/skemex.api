namespace Skemex.Application.Models.Projects;

public sealed class ProjectTaskCommentDto
{
    public Guid Id { get; init; }
    public Guid TaskId { get; init; }
    public Guid ProjectId { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ProjectTaskUserDto Author { get; init; } = null!;
}

