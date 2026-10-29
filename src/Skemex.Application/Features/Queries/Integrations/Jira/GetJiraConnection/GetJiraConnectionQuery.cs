using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.GetJiraConnection;

public sealed class GetJiraConnectionQuery : IQuery<JiraConnectionResponseDto>
{
    public Guid ProjectId { get; init; }
}
