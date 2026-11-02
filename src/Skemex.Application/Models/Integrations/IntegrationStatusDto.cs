namespace Skemex.Application.Models.Integrations;

public sealed class IntegrationStatusDto
{
    /// <summary>Normalized provider key (e.g. <c>jira</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>True when the provider is connected and ready to use (mapped project, etc.).</summary>
    public bool IsConfigured { get; init; }

    /// <summary>Whether any connection row exists for this provider.</summary>
    public bool HasConnection { get; init; }

    /// <summary>Raw connection status name, or <c>null</c> when no connection.</summary>
    public string? Status { get; init; }
}
