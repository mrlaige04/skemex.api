using System.Text.Json;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Domain.Consts;

namespace Skemex.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Stage-2 fallback for offtopic / unclear chat: returns a fixed polite reply without another LLM call.
/// </summary>
public sealed class UnsupportedRequestTool : IAgentTool
{
    public const string ToolSystemName = UnsupportedRequestToolDefaults.SystemName;

    public string SystemName => ToolSystemName;
    public string DefaultDescription => UnsupportedRequestToolDefaults.Description;
    public string DefaultSystemPrompt => UnsupportedRequestToolDefaults.SystemPrompt;
    public object? OutputSchema => null;

    public object ParameterSchema => JsonSerializer.Deserialize<object>("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {}
        }
        """)!;

    public Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Ok());

    public Task<AiToolExecutionResult> HandleFunctionCallAsync(
        string argumentsJson,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Ok());

    private static AiToolExecutionResult Ok() =>
        new(
            true,
            UnsupportedRequestToolDefaults.UserFacingReply,
            "chat",
            null,
            null);
}
