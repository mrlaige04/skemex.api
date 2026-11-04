using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Queries.Integrations.Jira.GetJiraEntityMappings;

public sealed class GetJiraEntityMappingsQuery : IQuery<JiraEntityMappingWorkspaceDto>
{
    public Guid ProjectId { get; init; }
    public string LocalEntityType { get; init; } = string.Empty;
}
