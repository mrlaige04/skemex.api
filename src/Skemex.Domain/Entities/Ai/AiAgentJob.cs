using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Ai;

public enum AiAgentJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

/// <summary>Unified background execution for agent tools / chat function calling.</summary>
public class AiAgentJob : TenantEntity
{
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid RequestedByUserId { get; set; }
    public User RequestedByUser { get; set; } = null!;

    public Guid? AiChatId { get; set; }
    public AiChat? AiChat { get; set; }

    /// <summary>When set, run that tool directly; when null, chat + function calling.</summary>
    public string? ToolName { get; set; }

    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
    public string? ArgumentsJson { get; set; }
    public string? Model { get; set; }

    public AiAgentJobStatus Status { get; set; } = AiAgentJobStatus.Pending;
    public string? Error { get; set; }
    public string? AssistantMessage { get; set; }
    public string? ArtifactType { get; set; }
    public string? ArtifactPayloadJson { get; set; }
    public string? HangfireJobId { get; set; }
}
