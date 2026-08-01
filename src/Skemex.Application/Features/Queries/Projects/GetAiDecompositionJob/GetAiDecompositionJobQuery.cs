using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Queries.Projects.GetAiDecompositionJob;

public sealed class GetAiDecompositionJobQuery : IQuery<AiDecompositionJobDto>
{
    public Guid ProjectId { get; set; }
    public Guid JobId { get; set; }
}
