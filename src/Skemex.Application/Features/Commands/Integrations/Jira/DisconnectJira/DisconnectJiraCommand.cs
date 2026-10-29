using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Integrations.Jira.DisconnectJira;

public sealed class DisconnectJiraCommand : ICommand
{
    public Guid ProjectId { get; init; }
}
