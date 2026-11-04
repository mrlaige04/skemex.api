using ErrorOr;
using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Integrations.Jira.DeleteJiraEntityMapping;

public sealed class DeleteJiraEntityMappingCommand : ICommand<Success>
{
    public Guid ProjectId { get; init; }
    public Guid MappingId { get; init; }
}
