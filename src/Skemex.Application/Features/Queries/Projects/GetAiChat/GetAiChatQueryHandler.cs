using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetAiChat;

public sealed class GetAiChatQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository)
    : IQueryHandler<GetAiChatQuery, AiChatDto>
{
    public async Task<ErrorOr<AiChatDto>> Handle(
        GetAiChatQuery request,
        CancellationToken cancellationToken)
    {
        var result = await AiChatAccess.GetOwnedChatAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            chatRepository,
            request.ProjectId,
            request.ChatId,
            cancellationToken,
            include: query => query
                .Include(chat => chat.AiModel)
                .Include(chat => chat.Messages)
                .ThenInclude(message => message.RootTask));
        if (result.IsError)
        {
            return result.Errors;
        }

        return AiChatDto.FromEntity(result.Value.Chat);
    }
}
