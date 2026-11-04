using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Services.Integrations.Jira;

public interface IJiraTaskImportService
{
    Task<ErrorOr<IReadOnlyList<JiraIssuePreviewDto>>> ListRemoteIssuesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<ImportTasksFromJiraResultDto>> ImportAsync(
        Guid projectId,
        ImportTasksFromJiraRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or refreshes a local task from a Jira webhook (no interactive user required).
    /// </summary>
    Task<ErrorOr<Guid>> UpsertIssueFromWebhookAsync(
        ProjectIntegration connection,
        string accessToken,
        string issueIdOrKey,
        CancellationToken cancellationToken = default);
}
