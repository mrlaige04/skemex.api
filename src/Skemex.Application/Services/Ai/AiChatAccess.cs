using ErrorOr;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Services.Ai;

public static class AiChatAccess
{
    public sealed record Context(Guid TenantId, Guid UserId);

    public static async Task<ErrorOr<Context>> EnsureMemberAsync(
        ICurrentUser currentUser,
        ITenantRepository<Project> projectRepository,
        ITenantRepository<ProjectUser> projectUserRepository,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before using AI chat.");
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before using AI chat.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == projectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var isMember = await projectUserRepository.ExistsAsync(
            filter: membership =>
                membership.ProjectId == projectId && membership.UserId == userId.Value,
            cancellationToken: cancellationToken);
        if (!isMember)
        {
            return Error.Forbidden(
                "Project.NotMember",
                "You must be a member of this project to use AI chat.");
        }

        return new Context(tenantId.Value, userId.Value);
    }

    public static async Task<ErrorOr<(Context Access, AiChat Chat)>> GetOwnedChatAsync(
        ICurrentUser currentUser,
        ITenantRepository<Project> projectRepository,
        ITenantRepository<ProjectUser> projectUserRepository,
        ITenantRepository<AiChat> chatRepository,
        Guid projectId,
        Guid chatId,
        CancellationToken cancellationToken,
        Func<IQueryable<AiChat>, IQueryable<AiChat>>? include = null)
    {
        var access = await EnsureMemberAsync(
            currentUser,
            projectRepository,
            projectUserRepository,
            projectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var chat = await chatRepository.GetAsync(
            filter: entry =>
                entry.Id == chatId
                && entry.ProjectId == projectId
                && entry.CreatedByUserId == access.Value.UserId,
            include: include,
            cancellationToken: cancellationToken);
        if (chat is null)
        {
            return Error.NotFound("AiChat.NotFound", "AI chat was not found.");
        }

        return (access.Value, chat);
    }
}
