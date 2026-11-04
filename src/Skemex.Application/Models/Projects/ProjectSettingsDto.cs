namespace Skemex.Application.Models.Projects;

public sealed class ProjectSettingsDto
{
    public Guid ProjectId { get; init; }
    public Guid DefaultTaskColumnId { get; init; }
    public Guid? DefaultAiModelId { get; init; }

    public static ProjectSettingsDto FromEntity(Domain.Entities.Projects.ProjectSettings settings) =>
        new()
        {
            ProjectId = settings.ProjectId,
            DefaultTaskColumnId = settings.DefaultTaskColumnId,
            DefaultAiModelId = settings.DefaultAiModelId,
        };
}
