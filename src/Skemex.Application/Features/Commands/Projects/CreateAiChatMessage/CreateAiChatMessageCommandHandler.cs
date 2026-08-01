using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChatMessage;

public sealed class CreateAiChatMessageCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository)
    : ICommandHandler<CreateAiChatMessageCommand, AiChatMessageDto>
{
    public async Task<ErrorOr<AiChatMessageDto>> Handle(
        CreateAiChatMessageCommand request,
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
        var message = new AiChatMessage
        {
            Id = Guid.NewGuid(),
            TenantId = result.Value.Access.TenantId,
            ChatId = chat.Id,
            Role = request.Role,
            Content = request.Content.Trim(),
        };

        await messageRepository.AddAsync(message, cancellationToken);
        await chatRepository.UpdateAsync(chat, cancellationToken);
        return AiChatMessageDto.FromEntity(message);
    }
}
