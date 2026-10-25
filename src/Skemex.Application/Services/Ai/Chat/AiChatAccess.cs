using ErrorOr;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Services.Ai.Chat;

public static class AiChatAccess
{
    public sealed record Context(Guid TenantId, Guid UserId);

    public static async Task<ErrorOr<Context>> EnsureMemberAsync(
        ICurrentUser currentUser,
        IProjectAccessService projectAccess,
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

        var access = await projectAccess.EnsureCanAccessProjectAsync(projectId, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return new Context(tenantId.Value, userId.Value);
    }

    public static async Task<ErrorOr<(Context Access, AiChat Chat)>> GetOwnedChatAsync(
        ICurrentUser currentUser,
        IProjectAccessService projectAccess,
        ITenantRepository<AiChat> chatRepository,
        Guid projectId,
        Guid chatId,
        CancellationToken cancellationToken,
        Func<IQueryable<AiChat>, IQueryable<AiChat>>? include = null)
    {
        var access = await EnsureMemberAsync(
            currentUser,
            projectAccess,
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

    // Back-compat overload used by callers that still pass repositories.
    public static Task<ErrorOr<Context>> EnsureMemberAsync(
        ICurrentUser currentUser,
        ITenantRepository<Project> projectRepository,
        ITenantRepository<ProjectUser> projectUserRepository,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        _ = projectRepository;
        _ = projectUserRepository;

        var access = new ProjectAccessService(currentUser, projectRepository, projectUserRepository);
        return EnsureMemberAsync(currentUser, access, projectId, cancellationToken);
    }

    public static Task<ErrorOr<(Context Access, AiChat Chat)>> GetOwnedChatAsync(
        ICurrentUser currentUser,
        ITenantRepository<Project> projectRepository,
        ITenantRepository<ProjectUser> projectUserRepository,
        ITenantRepository<AiChat> chatRepository,
        Guid projectId,
        Guid chatId,
        CancellationToken cancellationToken,
        Func<IQueryable<AiChat>, IQueryable<AiChat>>? include = null)
    {
        var access = new ProjectAccessService(currentUser, projectRepository, projectUserRepository);
        return GetOwnedChatAsync(
            currentUser,
            access,
            chatRepository,
            projectId,
            chatId,
            cancellationToken,
            include);
    }
}
