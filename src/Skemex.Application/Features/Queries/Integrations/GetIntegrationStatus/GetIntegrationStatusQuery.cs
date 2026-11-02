using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations;

namespace Skemex.Application.Features.Queries.Integrations.GetIntegrationStatus;

public sealed class GetIntegrationStatusQuery : IQuery<IntegrationStatusDto>
{
    public Guid ProjectId { get; init; }

    /// <summary>Provider key, e.g. <c>jira</c>.</summary>
    public required string Provider { get; init; }
}
