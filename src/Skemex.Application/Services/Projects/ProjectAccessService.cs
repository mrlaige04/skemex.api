using System.Linq.Expressions;
using ErrorOr;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Services.Projects;

public sealed class ProjectAccessService(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository) : IProjectAccessService
{
    public bool CanAccessAllProjects() => currentUser.HasPermission(PermissionNames.ProjectsViewAll);

    public async Task<bool> CanAccessProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (CanAccessAllProjects())
        {
            return await projectRepository.ExistsAsync(
                filter: p => p.Id == projectId,
                cancellationToken: cancellationToken);
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return false;
        }

        return await projectUserRepository.ExistsAsync(
            filter: m => m.ProjectId == projectId && m.UserId == userId.Value,
            cancellationToken: cancellationToken);
    }

    public async Task<ErrorOr<Success>> EnsureCanAccessProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before accessing projects.");
        }

        if (currentUser.GetUserId() is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before accessing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: p => p.Id == projectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        if (CanAccessAllProjects())
        {
            return Result.Success;
        }

        var canAccess = await CanAccessProjectAsync(projectId, cancellationToken);
        if (!canAccess)
        {
            return Error.Forbidden(
                "Project.NotMember",
                "You must be a member of this project to access it.");
        }

        return Result.Success;
    }

    public Expression<Func<Project, bool>> VisibilityFilter(Guid userId) =>
        p => p.Users.Any(u => u.UserId == userId);
}
