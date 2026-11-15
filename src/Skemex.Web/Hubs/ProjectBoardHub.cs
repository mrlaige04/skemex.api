using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Web.Hubs;

[Authorize]
public sealed class ProjectBoardHub(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository) : Hub
{
    public const string HubPath = "/hubs/project-board";
    public const string BoardChangedMethod = "boardChanged";

    public static string GroupName(Guid projectId) => $"project-board:{projectId:D}";

    public async Task JoinProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new HubException("Project id is required.");
        }

        if (currentUser.GetTenantId() is null || currentUser.GetUserId() is not Guid userId)
        {
            throw new HubException("Authentication required.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == projectId,
            cancellationToken: Context.ConnectionAborted);
        if (!projectExists)
        {
            throw new HubException("Project was not found.");
        }

        if (!currentUser.IsSuperAdmin())
        {
            var isMember = await projectUserRepository.ExistsAsync(
                filter: membership => membership.ProjectId == projectId && membership.UserId == userId,
                cancellationToken: Context.ConnectionAborted);
            if (!isMember)
            {
                throw new HubException("You are not a member of this project.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId), Context.ConnectionAborted);
    }

    public Task LeaveProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GroupName(projectId),
            Context.ConnectionAborted);
    }
}
