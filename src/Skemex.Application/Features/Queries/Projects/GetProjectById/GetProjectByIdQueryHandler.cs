using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectById;

public sealed class GetProjectByIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectByIdQuery, ProjectDto>
{
    public async Task<ErrorOr<ProjectDto>> Handle(
        GetProjectByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var project = await projectRepository.GetAsync(
            filter: p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);

        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Code = project.Code,
            Description = project.Description,
            LogoUrl = urlService.GetProjectLogoUrl(project.LogoBlobId),
            CreatedAt = project.CreatedAt,
        };
    }
}
