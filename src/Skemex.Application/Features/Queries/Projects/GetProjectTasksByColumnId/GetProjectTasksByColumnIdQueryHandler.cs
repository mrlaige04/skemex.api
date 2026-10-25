using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Helpers.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTasksByColumnId;

public sealed class GetProjectTasksByColumnIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTasksByColumnIdQuery, IReadOnlyList<ProjectTaskDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectTaskDto>>> Handle(
        GetProjectTasksByColumnIdQuery request,
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

        var columnExists = await projectColumnRepository.ExistsAsync(
            filter: column => column.Id == request.ColumnId && column.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!columnExists)
        {
            return Error.NotFound("ProjectColumn.NotFound", "Column was not found.");
        }

        var tasks = await projectTaskRepository.GetAllAsync(
            filter: task => task.ProjectId == request.ProjectId,
            include: query => query
                .Include(task => task.Assignee)
                .Include(task => task.Reporter),
            cancellationToken: cancellationToken);

        var avatarUrls = await ProjectTaskDtoHelper
            .LoadAvatarUrlsAsync(tasks, urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskDtoHelper.MapRootsWithSubtasks(tasks, request.ColumnId, avatarUrls).ToList();
    }
}
