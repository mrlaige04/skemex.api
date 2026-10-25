using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Ai;

/// <summary>
/// Central metering / audit record for every AI-related operation
/// (completions, function calls, embeddings, tool executions).
/// Correlation is polymorphic via optional FKs + <see cref="CorrelationId"/>.
/// </summary>
public class AiUsageActivity : TenantEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Groups related sub-operations under one user action
    /// (chat turn, UI click, background job). Independent of any single feature FK.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Soft reference to a parent activity (e.g. tool execution wrapping nested completions).
    /// Not an enforced FK so children can be written before the parent span is finalized.
    /// </summary>
    public Guid? ParentActivityId { get; set; }

    public AiUsageOperationType OperationType { get; set; }

    /// <summary>
    /// Stable feature/operation key for analytics
    /// (e.g. stage1_routing, task_decomposition, chunk_enrichment, rag_query_embed).
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ToolName { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    /// <summary>For embeddings: number of input texts in the batch.</summary>
    public int? InputItemCount { get; set; }

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    /// <summary>Extensible JSON bag for future billing metadata without schema churn.</summary>
    public string? MetadataJson { get; set; }

    // --- Optional polymorphic correlation FKs (all nullable) ---

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? AiChatId { get; set; }
    public AiChat? AiChat { get; set; }

    public Guid? AiChatMessageId { get; set; }
    public AiChatMessage? AiChatMessage { get; set; }

    public Guid? AiAgentJobId { get; set; }
    public AiAgentJob? AiAgentJob { get; set; }

    public Guid? ProjectDocumentId { get; set; }
    public ProjectDocument? ProjectDocument { get; set; }

    /// <summary>Parent execution (one <c>/api/ai/execute</c> run or equivalent job).</summary>
    public Guid? AiExecutionId { get; set; }
    public AiExecution? AiExecution { get; set; }
}
