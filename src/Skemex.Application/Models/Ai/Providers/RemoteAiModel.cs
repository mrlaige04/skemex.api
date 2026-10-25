namespace Skemex.Application.Models.Ai.Providers;

/// <summary>Remote model descriptor returned by a provider before persistence.</summary>
public sealed record RemoteAiModel(
    string ExternalId,
    string DisplayName,
    string? Author = null,
    string? IconKey = null);
