namespace Skemex.Application.Models.Ai.Completions;

public sealed record AiToolDefinition(
    string Name,
    string Description,
    object ParametersSchema);
