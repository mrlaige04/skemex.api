using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraIssues;

public sealed class ListJiraIssuesQuery : IQuery<IReadOnlyList<JiraIssuePreviewDto>>
{
    public Guid ProjectId { get; init; }
}
