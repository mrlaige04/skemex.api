using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.StartJiraOAuth;

public sealed class StartJiraOAuthCommand : ICommand<JiraAuthorizeStartDto>
{
    public Guid ProjectId { get; init; }
}
