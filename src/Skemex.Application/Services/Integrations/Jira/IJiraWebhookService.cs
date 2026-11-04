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
        ProjectIntegration connection,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task TryUnregisterAsync(
        ProjectIntegration connection,
        string? accessToken,
        CancellationToken cancellationToken = default);

    string? BuildCallbackUrl(ProjectIntegration connection);

    IReadOnlyList<JiraWebhookEventSettingDto> GetEventSettings(ProjectIntegration connection);

    void ApplyEventSettings(
        ProjectIntegration connection,
        IReadOnlyDictionary<string, bool> enabledByKey);

    /// <summary>
    /// Single inbound endpoint processor. Connection is resolved by <paramref name="token"/>.
    /// </summary>
    Task<ErrorOr<Success>> HandleInboundAsync(
        string? token,
        string rawJsonBody,
        CancellationToken cancellationToken = default);
}
