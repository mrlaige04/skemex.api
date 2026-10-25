using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Queries.Projects.GetProjectAiUsageSummary;

public sealed class GetProjectAiUsageSummaryQuery : IQuery<ProjectAiUsageSummaryDto>
{
    public Guid ProjectId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}
