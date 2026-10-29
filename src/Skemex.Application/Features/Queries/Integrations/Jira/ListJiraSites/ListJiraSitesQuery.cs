using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraSites;

public sealed class ListJiraSitesQuery : IQuery<IReadOnlyList<JiraAccessibleResourceDto>>
{
    public Guid ProjectId { get; init; }
}
