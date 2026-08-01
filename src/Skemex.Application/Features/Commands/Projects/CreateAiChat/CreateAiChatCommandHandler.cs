using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChat;

public sealed class CreateAiChatCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<AiChat> chatRepository,
    IBaseRepository<AiModel> aiModelRepository)
    : ICommandHandler<CreateAiChatCommand, AiChatDto>
{
    public async Task<ErrorOr<AiChatDto>> Handle(
        CreateAiChatCommand request,
        CancellationToken cancellationToken)
    {
        var access = await AiChatAccess.EnsureMemberAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        Guid? modelId = request.AiModelId;
        if (modelId is null)
        {
            var settings = await projectSettingsRepository.GetAsync(
                filter: entry => entry.ProjectId == request.ProjectId,
                cancellationToken: cancellationToken);
            modelId = settings?.DefaultAiModelId;
        }

        if (modelId is { } selectedModelId)
        {
            var modelExists = await aiModelRepository.ExistsAsync(
                filter: model => model.Id == selectedModelId && model.IsActive,
                cancellationToken: cancellationToken);
            if (!modelExists)
            {
                return Error.NotFound("AiModel.NotFound", "AI model was not found.");
            }
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? "New chat"
            : request.Title.Trim();

        var chat = new AiChat
        {
            Id = Guid.NewGuid(),
            TenantId = access.Value.TenantId,
            ProjectId = request.ProjectId,
            CreatedByUserId = access.Value.UserId,
            Title = title,
            AiModelId = modelId,
        };

        await chatRepository.AddAsync(chat, cancellationToken);

        var created = await chatRepository.GetAsync(
            filter: entry => entry.Id == chat.Id,
            include: query => query.Include(entry => entry.AiModel),
            cancellationToken: cancellationToken);

        return AiChatDto.FromEntity(created ?? chat);
    }
}
