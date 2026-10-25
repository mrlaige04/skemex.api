using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Services.Ai.Chat;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChat;

public sealed class UpdateAiChatCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    IBaseRepository<AiModel> aiModelRepository)
    : ICommandHandler<UpdateAiChatCommand, AiChatSummaryDto>
{
    public async Task<ErrorOr<AiChatSummaryDto>> Handle(
        UpdateAiChatCommand request,
        CancellationToken cancellationToken)
    {
        var result = await AiChatAccess.GetOwnedChatAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            chatRepository,
            request.ProjectId,
            request.ChatId,
            cancellationToken);
        if (result.IsError)
        {
            return result.Errors;
        }

        var chat = result.Value.Chat;

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            chat.Title = request.Title.Trim();
        }

        if (request.ClearAiModel)
        {
            chat.AiModelId = null;
        }
        else if (request.AiModelId is { } modelId)
        {
            var modelExists = await aiModelRepository.ExistsAsync(
                filter: model => model.Id == modelId && model.IsActive,
                cancellationToken: cancellationToken);
            if (!modelExists)
            {
                return Error.NotFound("AiModel.NotFound", "AI model was not found.");
            }

            chat.AiModelId = modelId;
        }

        await chatRepository.UpdateAsync(chat, cancellationToken);

        var updated = await chatRepository.GetAsync(
            filter: entry => entry.Id == chat.Id,
            include: query => query.Include(entry => entry.AiModel),
            cancellationToken: cancellationToken);

        return AiChatSummaryDto.FromEntity(updated ?? chat);
    }
}
