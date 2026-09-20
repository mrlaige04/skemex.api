using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAgentTools;

namespace Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentTools;

public sealed class GetSaAgentToolsQuery : IQuery<IReadOnlyList<SaAgentToolSummaryDto>>
{
    public string? Search { get; set; }
}
