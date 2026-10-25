using System.Text.Json;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Agents;

/// <summary>Isolated agent capability that can run directly or via LLM function calling.</summary>
public interface IAgentTool
{
    string SystemName { get; }
    string DefaultDescription { get; }
    string DefaultSystemPrompt { get; }
    object ParameterSchema { get; }

    /// <summary>
    /// Optional JSON Schema for the Stage-2 model response. Null for unstructured (text/markdown) tools.
    /// </summary>
    object? OutputSchema => null;

    Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<AiToolExecutionResult> HandleFunctionCallAsync(
        string argumentsJson,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional project-scoped runtime dataset for prompt injection (e.g. team tables).
    /// Return null/whitespace when the tool has nothing to contribute.
    /// </summary>
    Task<string?> BuildDynamicContextAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Side-effect-free check of function-call arguments before execution.
    /// Return null when valid; otherwise a reason suitable for semantic retry logging.
    /// </summary>
    string? ValidateFunctionCallArguments(
        string argumentsJson,
        AgentExecutionContext context)
        => null;
}
