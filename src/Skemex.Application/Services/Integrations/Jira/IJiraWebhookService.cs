using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Services.Integrations.Jira;

public interface IJiraWebhookService
{
    /// <summary>
    /// Ensures secret + defaults + a single Jira webhook registered for ALL supported events.
    /// Per-event enable/disable is local-only and does not re-register in Jira.
    /// </summary>
    Task EnsureRegisteredAsync(
        IntegrationConnection connection,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task TryUnregisterAsync(
        IntegrationConnection connection,
        string? accessToken,
        CancellationToken cancellationToken = default);

    string? BuildCallbackUrl(IntegrationConnection connection);

    IReadOnlyList<JiraWebhookEventSettingDto> GetEventSettings(IntegrationConnection connection);

    void ApplyEventSettings(
        IntegrationConnection connection,
        IReadOnlyDictionary<string, bool> enabledByKey);

    /// <summary>
    /// Single inbound endpoint processor. Connection is resolved by <paramref name="token"/>.
    /// </summary>
    Task<ErrorOr<Success>> HandleInboundAsync(
        string? token,
        string rawJsonBody,
        CancellationToken cancellationToken = default);
}
