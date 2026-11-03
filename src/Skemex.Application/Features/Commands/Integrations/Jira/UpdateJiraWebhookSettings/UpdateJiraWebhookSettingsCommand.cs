using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.UpdateJiraWebhookSettings;

public sealed class UpdateJiraWebhookSettingsCommand : ICommand<JiraConnectionDto>
{
    public Guid ProjectId { get; init; }
    public Dictionary<string, bool> Events { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
