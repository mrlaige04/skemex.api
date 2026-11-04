using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Common;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskComments;

public sealed class GetProjectTaskCommentsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskComment> commentRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTaskCommentsQuery, IReadOnlyList<ProjectTaskCommentDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectTaskCommentDto>>> Handle(
        GetProjectTaskCommentsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var taskExists = await projectTaskRepository.ExistsAsync(
            filter: task => task.Id == request.TaskId && task.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!taskExists)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var entries = await commentRepository.GetAllAsync(
            filter: comment => comment.TaskId == request.TaskId && comment.ProjectId == request.ProjectId,
            include: query => query.Include(comment => comment.Author),
            cancellationToken: cancellationToken);

        var ordered = entries
            .OrderByDescending(comment => comment.CreatedAt)
            .ToList();

        var avatars = await ProjectTaskCommentDtoHelper
            .LoadAvatarUrlsAsync(ordered, urlService, cancellationToken)
            .ConfigureAwait(false);

        return ordered
            .Select(comment => ProjectTaskCommentDtoHelper.Map(comment, avatars))
            .ToList();
    }
}
