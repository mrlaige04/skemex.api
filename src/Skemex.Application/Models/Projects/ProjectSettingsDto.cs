namespace Skemex.Application.Models.Projects;

public sealed class ProjectSettingsDto
{
    public Guid ProjectId { get; init; }
    public Guid DefaultTaskColumnId { get; init; }
}
