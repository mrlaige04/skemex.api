using Skemex.Domain.Enums;
using Skemex.Domain.Events.Abstractions;

namespace Skemex.Domain.Events.Ai;

/// <summary>Raised when an AI agent job / execution reaches a terminal status.</summary>
public sealed class AiExecutionFinishedDomainEvent : IDomainEvent
{
    public required Guid AiExecutionId { get; init; }
    public required Guid AiAgentJobId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public required AiExecutionStatus Status { get; init; }
    public string? ToolName { get; init; }
    public string? ArtifactType { get; init; }
    public string? AssistantMessage { get; init; }
    public string? UserInput { get; init; }
    public string? Error { get; init; }
}
