using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Agents;

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
