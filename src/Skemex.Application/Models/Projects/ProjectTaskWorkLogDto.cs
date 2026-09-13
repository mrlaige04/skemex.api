namespace Skemex.Application.Models.Projects;

public sealed class ProjectTaskWorkLogDto
{
    public Guid Id { get; init; }
    public Guid TaskId { get; init; }
    public Guid ProjectId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int SpentMinutes { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ProjectTaskUserDto User { get; init; } = null!;
}
