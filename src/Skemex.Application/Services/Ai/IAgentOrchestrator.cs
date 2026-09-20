using System.Text.Json;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Services.Ai;

public interface IAgentOrchestrator
{
    /// <summary>Schedules a Hangfire job that runs <see cref="ProcessJobAsync"/>.</summary>
    string Enqueue(Guid agentJobId, Guid tenantId, Guid requestedByUserId);

    /// <summary>Hangfire entry: loads the job, runs the orchestrator, persists status/messages.</summary>
    Task ProcessJobAsync(
        Guid agentJobId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Direct tool run when <paramref name="request"/>.ToolName is set; otherwise chat + function calling.
    /// </summary>
    Task<AiToolExecutionResult> ExecuteAsync(
        AgentOrchestratorRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AgentOrchestratorRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public Guid? DecompositionJobId { get; init; }
    public Guid? AgentJobId { get; init; }

    /// <summary>When set, runs that tool via <see cref="IAgentTool.ExecuteDirectAsync"/>.</summary>
    public string? ToolName { get; init; }

    public required string UserInput { get; init; }
    public string? CustomInstructions { get; init; }
    public string? Model { get; init; }

    /// <summary>Optional JSON object of direct tool arguments (merged with userInput).</summary>
    public JsonElement? DirectArgs { get; init; }

    /// <summary>Optional raw JSON arguments string from the API/job payload.</summary>
    public string? ArgumentsJson { get; init; }
}
