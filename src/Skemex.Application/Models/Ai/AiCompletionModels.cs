namespace Skemex.Application.Models.Ai;

public sealed record AiToolDefinition(
    string Name,
    string Description,
    object ParametersSchema);

public sealed record AiFunctionCall(
    string Name,
    string ArgumentsJson);

/// <summary>Token usage returned by the provider when available.</summary>
public sealed class AiTokenUsage
{
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
}

/// <summary>Plain text/JSON completion (no tools). Used by agent tools in <c>ExecuteDirectAsync</c>.</summary>
public sealed class AiCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string Model { get; init; }
    public bool PreferJsonObject { get; init; } = true;
}

public sealed class AiCompletionResult
{
    public required string Content { get; init; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public AiTokenUsage? Usage { get; set; }
}

/// <summary>Chat + function-calling request. Used by the UI chat flow.</summary>
public sealed class AiFunctionCallRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string Model { get; init; }
    public required IReadOnlyList<AiToolDefinition> Tools { get; init; }
}

public sealed class AiFunctionCallResult
{
    public string? Content { get; init; }
    public AiFunctionCall? FunctionCall { get; init; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public AiTokenUsage? Usage { get; set; }

    public bool HasFunctionCall => FunctionCall is not null;
}
