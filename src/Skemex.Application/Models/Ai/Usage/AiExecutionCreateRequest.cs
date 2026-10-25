using Skemex.Domain.Enums;

namespace Skemex.Application.Models.Ai.Usage;

public sealed class AiExecutionCreateRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? AiChatId { get; init; }
    public Guid? AiAgentJobId { get; init; }
    public Guid? ProjectDocumentId { get; init; }
    public required AiExecutionSource Source { get; init; }
    public string? ToolName { get; init; }
    public string? Model { get; init; }
    public string? UserInput { get; init; }
    public string? Title { get; init; }
}
