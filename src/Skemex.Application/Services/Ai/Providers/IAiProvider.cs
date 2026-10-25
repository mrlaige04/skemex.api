using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Providers;

/// <summary>
/// One concrete models API (Groq, OpenRouter, …).
/// Implementations list models and complete prompts; prefer Microsoft.Extensions.AI
/// <c>IChatClient</c> internally when the vendor supports it, otherwise call the vendor REST API.
/// </summary>
public interface IAiProvider
{
    /// <summary>Provider key (slug), stored on <c>ai_models.Provider</c>.</summary>
    string Name { get; }

    /// <summary>Human-readable provider name for UI grouping.</summary>
    string DisplayName { get; }

    Task<IReadOnlyList<RemoteAiModel>> ListModelsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Plain completion without tools (direct agent tool execution).</summary>
    Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Chat completion with tool schemas for function calling.</summary>
    Task<AiFunctionCallResult> FunctionCallAsync(
        AiFunctionCallRequest request,
        CancellationToken cancellationToken = default);
}
