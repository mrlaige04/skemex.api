using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Models.Ai;

public sealed class AiDecompositionJobDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
    public string? Error { get; set; }
    public Guid? RootTaskId { get; set; }
    public string? RootTaskCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static AiDecompositionJobDto FromEntity(AiDecompositionJob job) =>
        new()
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            Status = job.Status.ToString(),
            UserInput = job.UserInput,
            CustomInstructions = job.CustomInstructions,
            Error = job.Error,
            RootTaskId = job.RootTaskId,
            RootTaskCode = job.RootTask?.Code,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
        };
}
