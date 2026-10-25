using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Enums;

namespace Skemex.Application.Services.Ai.Usage;

public interface IAiExecutionLifecycle
{
    Task<AiExecution> CreatePendingAsync(
        AiExecutionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task MarkRunningAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid executionId,
        AiExecutionCompleteRequest request,
        CancellationToken cancellationToken = default);
}

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

public sealed class AiExecutionCompleteRequest
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Title { get; init; }
    public string? ToolName { get; init; }
    public string? Model { get; init; }
}
