using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteAiChatMessage;

public sealed class DeleteAiChatMessageCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository)
    : ICommandHandler<DeleteAiChatMessageCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteAiChatMessageCommand request,
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
            cancellationToken: cancellationToken);
        if (message is null)
        {
            return Error.NotFound("AiChatMessage.NotFound", "Message was not found.");
        }

        await messageRepository.DeleteAsync(message, cancellationToken);
        await chatRepository.UpdateAsync(owned.Value.Chat, cancellationToken);
        return Result.Success;
    }
}
