using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Ai;

/// <summary>
/// One top-level AI execution (typically one <c>POST /api/ai/execute</c> job).
/// Nested metered steps are stored as <see cref="AiUsageActivity"/> rows linked via <c>AiExecutionId</c>.
/// </summary>
public class AiExecution : TenantEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? AiChatId { get; set; }
    public AiChat? AiChat { get; set; }

    public Guid? AiAgentJobId { get; set; }
    public AiAgentJob? AiAgentJob { get; set; }

    public Guid? ProjectDocumentId { get; set; }
    public ProjectDocument? ProjectDocument { get; set; }

    public AiExecutionSource Source { get; set; }
    public AiExecutionStatus Status { get; set; } = AiExecutionStatus.Pending;

    /// <summary>Resolved display title (e.g. Project Q&amp;A, Task decomposition).</summary>
    public string Title { get; set; } = "AI execution";

    public string? ToolName { get; set; }
    public string? Model { get; set; }
    public string? UserInputPreview { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long? DurationMs { get; set; }

    public bool? Success { get; set; }
    public string? ErrorMessage { get; set; }

    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public int OperationCount { get; set; }

    public ICollection<AiUsageActivity> Activities { get; set; } = [];
}
