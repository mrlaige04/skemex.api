using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.MapJiraProject;

public sealed class MapJiraProjectCommand : ICommand<JiraConnectionDto>
{
    public Guid ProjectId { get; init; }
    public string CloudId { get; init; } = string.Empty;
    public string? SiteUrl { get; init; }
    public string? SiteName { get; init; }
    public string ExternalProjectId { get; init; } = string.Empty;
    public string ExternalProjectKey { get; init; } = string.Empty;
    public string ExternalProjectName { get; init; } = string.Empty;
}
