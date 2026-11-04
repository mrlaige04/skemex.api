using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Features.Commands.Integrations.Jira.UpsertJiraEntityMapping;

public sealed class UpsertJiraEntityMappingCommand : ICommand<JiraMappingLinkDto>
{
    public Guid ProjectId { get; init; }
    public string LocalEntityType { get; init; } = string.Empty;
    public Guid LocalEntityId { get; init; }
    public string ExternalEntityId { get; init; } = string.Empty;
    public string? ExternalEntityKey { get; init; }
    public string? Status { get; init; }
}
