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
                .ThenInclude(message => message.RootTask)
                .Include(chat => chat.Messages)
                .ThenInclude(message => message.Attachments));
        if (result.IsError)
        {
            return result.Errors;
        }

        return AiChatDto.FromEntity(result.Value.Chat);
    }
}
