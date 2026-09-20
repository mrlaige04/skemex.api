using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAgentTools;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentToolById;

public sealed class GetSaAgentToolByIdQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<AgentTool> agentToolRepository)
    : IQueryHandler<GetSaAgentToolByIdQuery, SaAgentToolDto>
{
    public async Task<ErrorOr<SaAgentToolDto>> Handle(
        GetSaAgentToolByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var tool = await agentToolRepository.GetAsync(
            filter: entry => entry.Id == request.ToolId,
            cancellationToken: cancellationToken);

        if (tool is null)
        {
            return Error.NotFound("AgentTool.NotFound", "Agent tool was not found.");
        }

        return ToDto(tool);
    }

    internal static SaAgentToolDto ToDto(AgentTool tool) =>
        new()
        {
            Id = tool.Id,
            SystemName = tool.SystemName,
            Description = tool.Description,
            SystemPrompt = tool.SystemPrompt,
            CreatedAt = tool.CreatedAt,
            UpdatedAt = tool.UpdatedAt,
        };
}
