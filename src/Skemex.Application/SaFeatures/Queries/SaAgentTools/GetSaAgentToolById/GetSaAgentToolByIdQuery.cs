using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAgentTools;

namespace Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentToolById;

public sealed class GetSaAgentToolByIdQuery : IQuery<SaAgentToolDto>
{
    public Guid ToolId { get; init; }
}
