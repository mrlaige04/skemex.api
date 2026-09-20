using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentToolById;
using Skemex.Application.SaModels.SaAgentTools;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.SaAgentTools.UpdateSaAgentTool;

public sealed class UpdateSaAgentToolCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AgentTool> agentToolRepository)
    : ICommandHandler<UpdateSaAgentToolCommand, SaAgentToolDto>
{
    public async Task<ErrorOr<SaAgentToolDto>> Handle(
        UpdateSaAgentToolCommand request,
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

        var changed = false;

        if (request.Description is not null)
        {
            var description = request.Description.Trim();
            if (description.Length > 0
                && !string.Equals(description, tool.Description, StringComparison.Ordinal))
            {
                tool.Description = description;
                changed = true;
            }
        }

        if (request.SystemPrompt is not null)
        {
            var systemPrompt = request.SystemPrompt.Trim();
            if (systemPrompt.Length > 0
                && !string.Equals(systemPrompt, tool.SystemPrompt, StringComparison.Ordinal))
            {
                tool.SystemPrompt = systemPrompt;
                changed = true;
            }
        }

        if (changed)
        {
            tool.UpdatedAt = DateTime.UtcNow;
            await agentToolRepository.UpdateAsync(tool, cancellationToken);
        }

        return GetSaAgentToolByIdQueryHandler.ToDto(tool);
    }
}
