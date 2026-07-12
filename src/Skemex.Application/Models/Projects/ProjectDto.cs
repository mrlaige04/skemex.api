namespace Skemex.Application.Models.Projects;

public sealed class ProjectDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public DateTime CreatedAt { get; init; }
}
