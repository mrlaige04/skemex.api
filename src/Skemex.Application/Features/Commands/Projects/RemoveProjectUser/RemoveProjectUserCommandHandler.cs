using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.RemoveProjectUser;

public sealed class RemoveProjectUserCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository)
    : ICommandHandler<RemoveProjectUserCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        RemoveProjectUserCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var membership = await projectUserRepository.GetAsync(
            filter: pu => pu.ProjectId == request.ProjectId && pu.UserId == request.UserId,
            cancellationToken: cancellationToken);
        if (membership is null)
        {
            return Error.NotFound("Project.UserNotFound", "User is not a member of this project.");
        }

        var memberCount = await projectUserRepository.CountAsync(
            filter: pu => pu.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (memberCount <= 1)
        {
            return Error.Validation(
                "Project.LastUser",
                "A project must keep at least one member.");
        }

        await projectUserRepository.DeleteAsync(membership, cancellationToken);
        return Result.Success;
    }
}
