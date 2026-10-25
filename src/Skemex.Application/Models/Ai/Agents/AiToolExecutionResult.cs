namespace Skemex.Application.Models.Ai.Agents;

public sealed record AiToolExecutionResult(
    bool Success,
    string? AssistantMessage,
    string ArtifactType,
    object? ArtifactPayload,
    string? ErrorMessage = null);
