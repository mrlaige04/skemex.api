using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteAiChat;

public sealed class DeleteAiChatCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository)
    : ICommandHandler<DeleteAiChatCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteAiChatCommand request,
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

        await chatRepository.DeleteAsync(result.Value.Chat, cancellationToken);
        return Result.Success;
    }
}
