using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraUsers;

public sealed class ListJiraUsersQuery : IQuery<IReadOnlyList<JiraUserDto>>
{
    public Guid ProjectId { get; init; }
}
