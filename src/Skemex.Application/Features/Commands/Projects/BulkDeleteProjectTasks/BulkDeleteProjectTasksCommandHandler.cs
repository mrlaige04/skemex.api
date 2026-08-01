using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.BulkDeleteProjectTasks;

public sealed class BulkDeleteProjectTasksCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository)
    : ICommandHandler<BulkDeleteProjectTasksCommand>
{
    private const int MaxBatchSize = 100;

    public async Task<ErrorOr<Success>> Handle(
        BulkDeleteProjectTasksCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var ids = request.TaskIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return Error.Validation("ProjectTask.IdsRequired", "Select at least one issue to delete.");
        }

        if (ids.Count > MaxBatchSize)
        {
            return Error.Validation(
                "ProjectTask.TooMany",
                $"You can delete at most {MaxBatchSize} issues at once.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var tasks = await projectTaskRepository.GetAllAsync(
            filter: task => task.ProjectId == request.ProjectId && ids.Contains(task.Id),
            cancellationToken: cancellationToken);

        if (tasks.Count == 0)
        {
            return Error.NotFound("ProjectTask.NotFound", "No matching issues were found.");
        }

        await projectTaskRepository.DeleteRangeAsync(tasks, cancellationToken);
        return Result.Success;
    }
}
