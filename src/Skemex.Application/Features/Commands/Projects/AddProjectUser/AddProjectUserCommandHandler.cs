using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.AddProjectUser;

public sealed class AddProjectUserCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<TenantUser> tenantUserRepository,
    IUrlService urlService)
    : ICommandHandler<AddProjectUserCommand, ProjectUserDto>
{
    public async Task<ErrorOr<ProjectUserDto>> Handle(
        AddProjectUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
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

        var tenantUser = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == request.UserId && tu.Status == TenantUserStatus.Active,
            include: q => q.Include(tu => tu.User),
            cancellationToken: cancellationToken);
        if (tenantUser is null)
        {
            return Error.Validation(
                "Project.UserNotInTenant",
                "User must be an active member of this workspace.");
        }

        var alreadyMember = await projectUserRepository.ExistsAsync(
            pu => pu.ProjectId == request.ProjectId && pu.UserId == request.UserId,
            cancellationToken: cancellationToken);
        if (alreadyMember)
        {
            return Error.Conflict("Project.UserAlreadyMember", "User is already a member of this project.");
        }

        var membership = new ProjectUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            UserId = request.UserId,
        };

        await projectUserRepository.AddAsync(membership, cancellationToken);

        return new ProjectUserDto
        {
            Id = tenantUser.User.Id,
            Email = tenantUser.User.Email ?? string.Empty,
            FirstName = tenantUser.User.FirstName,
            LastName = tenantUser.User.LastName,
            AvatarUrl = await urlService
                .GetUserProfilePictureUrlAsync(tenantUser.User.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
        };
    }
}
