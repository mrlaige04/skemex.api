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
                .Include(chat => chat.AiModel)
                .OrderByDescending(chat => chat.UpdatedAt ?? chat.CreatedAt)
                .ThenByDescending(chat => chat.CreatedAt),
            cancellationToken: cancellationToken);

        return chats.Select(AiChatSummaryDto.FromEntity).ToList();
    }
}
