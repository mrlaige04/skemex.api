using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Ai;

public enum AiDecompositionJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

public class AiDecompositionJob : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public Guid? AiChatId { get; set; }
    public AiChat? AiChat { get; set; }

    public Guid? UserMessageId { get; set; }
    public AiChatMessage? UserMessage { get; set; }

    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }

    public AiDecompositionJobStatus Status { get; set; } = AiDecompositionJobStatus.Pending;
    public string? Error { get; set; }
    public Guid? RootTaskId { get; set; }
    public ProjectTask? RootTask { get; set; }
    public string? HangfireJobId { get; set; }
}
