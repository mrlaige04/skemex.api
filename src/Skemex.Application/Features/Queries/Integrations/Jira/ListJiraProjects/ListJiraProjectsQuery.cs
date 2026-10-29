using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraProjects;

public sealed class ListJiraProjectsQuery : IQuery<IReadOnlyList<JiraProjectDto>>
{
    public Guid ProjectId { get; init; }
    public string? CloudId { get; init; }
}
