using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChatMessage;

public sealed class UpdateAiChatMessageCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository)
    : ICommandHandler<UpdateAiChatMessageCommand, AiChatMessageDto>
{
    public async Task<ErrorOr<AiChatMessageDto>> Handle(
        UpdateAiChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        var owned = await AiChatAccess.GetOwnedChatAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            chatRepository,
            request.ProjectId,
            request.ChatId,
            cancellationToken);
        if (owned.IsError)
        {
            return owned.Errors;
        }

        var message = await messageRepository.GetAsync(
            filter: entry =>
                entry.Id == request.MessageId && entry.ChatId == request.ChatId,
            include: query => query.Include(entry => entry.RootTask),
            cancellationToken: cancellationToken);
        if (message is null)
        {
            return Error.NotFound("AiChatMessage.NotFound", "Message was not found.");
        }

        message.Content = request.Content.Trim();
        await messageRepository.UpdateAsync(message, cancellationToken);
        await chatRepository.UpdateAsync(owned.Value.Chat, cancellationToken);
        return AiChatMessageDto.FromEntity(message);
    }
}
