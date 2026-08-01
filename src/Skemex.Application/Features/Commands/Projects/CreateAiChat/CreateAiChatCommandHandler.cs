using ErrorOr;
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
    ITenantRepository<AiChat> chatRepository)
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
        };

        await chatRepository.AddAsync(chat, cancellationToken);
        return AiChatDto.FromEntity(chat);
    }
}
