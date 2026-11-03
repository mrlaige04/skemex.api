using System.Text.Json;
using ErrorOr;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Services.Integrations.Jira.Webhooks;

public interface IJiraWebhookEventHandler
{
    /// <summary>Stable Skemex event key from <see cref="JiraWebhookEventCatalog"/>.</summary>
    string EventKey { get; }

    Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default);
}

public sealed class JiraWebhookEventContext
{
    public required IntegrationConnection Connection { get; init; }
    public required string EventKey { get; init; }
    public required string JiraEventName { get; init; }
    public required JsonElement Payload { get; init; }
    public required string AccessToken { get; init; }
}
