using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Domain.Enums;

namespace Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;

public sealed class EnqueueAiAgentExecutionCommand : ICommand<AiAgentJobDto>
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public string? ToolName { get; init; }
    public required string UserInput { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? Model { get; init; }

    /// <summary>Optional explicit source; inferred from chat/tool when null.</summary>
    public AiExecutionSource? Source { get; init; }
}
