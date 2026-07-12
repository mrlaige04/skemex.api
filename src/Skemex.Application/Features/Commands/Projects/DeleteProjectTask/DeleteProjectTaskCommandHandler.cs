using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTask;

public sealed class DeleteProjectTaskCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectTask> projectTaskRepository)
    : ICommandHandler<DeleteProjectTaskCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectTaskCommand request,
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

        var task = await projectTaskRepository.GetAsync(
            filter: entry =>
                entry.Id == request.TaskId
                && entry.ProjectId == request.ProjectId
                && entry.ProjectColumnId == request.ColumnId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        await projectTaskRepository.DeleteAsync(task, cancellationToken);
        return Result.Success;
    }
}
