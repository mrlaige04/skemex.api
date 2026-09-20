using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;

public sealed class EnqueueAiAgentExecutionCommand : ICommand<AiAgentJobDto>
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public string? ToolName { get; init; }
    public required string UserInput { get; init; }
    public string? CustomInstructions { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? Model { get; init; }
}
