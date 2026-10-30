using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.ImportTasksFromJira;

public sealed class ImportTasksFromJiraCommand : ICommand<ImportTasksFromJiraResultDto>
{
    public Guid ProjectId { get; init; }
    public bool ImportAll { get; init; }
    public IReadOnlyList<string> IssueKeys { get; init; } = [];
    public bool SkipAlreadyImported { get; init; } = true;
}
