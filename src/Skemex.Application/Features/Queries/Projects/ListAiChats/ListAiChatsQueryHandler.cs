using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.ListAiChats;

public sealed class ListAiChatsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<AiChat> chatRepository)
    : IQueryHandler<ListAiChatsQuery, IReadOnlyList<AiChatSummaryDto>>
{
    public async Task<ErrorOr<IReadOnlyList<AiChatSummaryDto>>> Handle(
        ListAiChatsQuery request,
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

        var chats = await chatRepository.GetAllAsync(
            filter: chat =>
                chat.ProjectId == request.ProjectId && chat.CreatedByUserId == access.Value.UserId,
            include: query => query
                .OrderByDescending(chat => chat.UpdatedAt ?? chat.CreatedAt)
                .ThenByDescending(chat => chat.CreatedAt),
            cancellationToken: cancellationToken);

        return chats.Select(AiChatSummaryDto.FromEntity).ToList();
    }
}
