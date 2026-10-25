using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Enums;

namespace Skemex.Application.Features.Queries.Projects.GetProjectAiUsageActivities;

public static class AiUsageActivitySort
{
    public const string CreatedAtDesc = "createdAtDesc";
    public const string CreatedAtAsc = "createdAtAsc";
    public const string TotalTokensDesc = "totalTokensDesc";
    public const string TotalTokensAsc = "totalTokensAsc";
    public const string DurationMsDesc = "durationMsDesc";
    public const string DurationMsAsc = "durationMsAsc";
}

public sealed class GetProjectAiUsageActivitiesQuery : IQuery<PaginatedList<AiUsageActivityDto>>
{
    public Guid ProjectId { get; init; }
    public string? Search { get; init; }
    public AiUsageOperationType? OperationType { get; init; }
    public string? OperationName { get; init; }
    public bool? Success { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string Sort { get; init; } = AiUsageActivitySort.CreatedAtDesc;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
