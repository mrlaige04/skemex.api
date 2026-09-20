namespace Skemex.Application.Models.Ai;

public sealed class AiAgentJobDto
{
    public Guid Id { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ToolName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string UserInput { get; init; } = string.Empty;
    public string? CustomInstructions { get; init; }
    public string? Error { get; init; }
    public string? AssistantMessage { get; init; }
    public string? ArtifactType { get; init; }
    public string? ArtifactPayloadJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public static AiAgentJobDto FromEntity(Domain.Entities.Ai.AiAgentJob job) => new()
    {
        Id = job.Id,
        ProjectId = job.ProjectId,
        ToolName = job.ToolName,
        Status = job.Status.ToString(),
        UserInput = job.UserInput,
        CustomInstructions = job.CustomInstructions,
        Error = job.Error,
        AssistantMessage = job.AssistantMessage,
        ArtifactType = job.ArtifactType,
        ArtifactPayloadJson = job.ArtifactPayloadJson,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt,
    };
}
