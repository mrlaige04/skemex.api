namespace Skemex.Application.Models.Ai.Completions;

public sealed record AiFunctionCall(
    string Name,
    string ArgumentsJson);
