using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;

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
}
