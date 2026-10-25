namespace Skemex.Application.Models.Ai.Usage;

public sealed class AiExecutionCompleteRequest
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Title { get; init; }
    public string? ToolName { get; init; }
    public string? Model { get; init; }
}
