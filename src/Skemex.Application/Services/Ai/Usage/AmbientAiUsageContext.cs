using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Usage;

/// <summary>
/// AsyncLocal correlation for nested AI / embedding / tool calls.
/// Set at the entry of a user action or background job; metering decorators read it.
/// </summary>
public static class AmbientAiUsageContext
{
    private static readonly AsyncLocal<AiUsageCorrelation?> CurrentLocal = new();

    public static AiUsageCorrelation? Current => CurrentLocal.Value;

    public static IDisposable Use(AiUsageCorrelation correlation) =>
        new Scope(correlation);

    public static IDisposable Use(
        Guid correlationId,
        Guid? projectId = null,
        Guid? aiChatId = null,
        Guid? aiChatMessageId = null,
        Guid? aiAgentJobId = null,
        Guid? projectDocumentId = null,
        Guid? aiExecutionId = null,
        string? operationName = null,
        string? toolName = null,
        Guid? parentActivityId = null) =>
        Use(new AiUsageCorrelation
        {
            CorrelationId = correlationId,
            ProjectId = projectId,
            AiChatId = aiChatId,
            AiChatMessageId = aiChatMessageId,
            AiAgentJobId = aiAgentJobId,
            ProjectDocumentId = projectDocumentId,
            AiExecutionId = aiExecutionId,
            OperationName = operationName,
            ToolName = toolName,
            ParentActivityId = parentActivityId,
        });

    /// <summary>Overlays fields on the current correlation (or starts one).</summary>
    public static IDisposable Push(
        string? operationName = null,
        string? toolName = null,
        Guid? parentActivityId = null,
        Guid? projectDocumentId = null)
    {
        var current = Current ?? new AiUsageCorrelation { CorrelationId = Guid.NewGuid() };
        return Use(current with
        {
            OperationName = operationName ?? current.OperationName,
            ToolName = toolName ?? current.ToolName,
            ParentActivityId = parentActivityId ?? current.ParentActivityId,
            ProjectDocumentId = projectDocumentId ?? current.ProjectDocumentId,
        });
    }

    private sealed class Scope : IDisposable
    {
        private readonly AiUsageCorrelation? _previous;
        private bool _disposed;

        public Scope(AiUsageCorrelation correlation)
        {
            _previous = CurrentLocal.Value;
            CurrentLocal.Value = correlation;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentLocal.Value = _previous;
            _disposed = true;
        }
    }
}
