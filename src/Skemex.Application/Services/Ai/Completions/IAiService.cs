using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Completions;

/// <summary>
/// Application façade for AI: plain completion, function calling, and model catalog.
/// </summary>
public interface IAiService
{
    /// <summary>Plain completion (no tools). Used by <c>IAgentTool.ExecuteDirectAsync</c>.</summary>
    Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chat + function calling. Used when the UI chat lets the model pick a tool.</summary>
    Task<AiFunctionCallResult> FunctionCallAsync(
        AiFunctionCallRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists active models from the local catalog (read-only; does not sync providers).</summary>
    Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
