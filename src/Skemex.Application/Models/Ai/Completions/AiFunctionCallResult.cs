namespace Skemex.Application.Models.Ai.Completions;

public sealed class AiFunctionCallResult
{
    public string? Content { get; init; }
    public AiFunctionCall? FunctionCall { get; init; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public AiTokenUsage? Usage { get; set; }

    public bool HasFunctionCall => FunctionCall is not null;
}
