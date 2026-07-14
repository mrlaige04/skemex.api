using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProject;

public sealed class UpdateProjectCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    IUrlService urlService)
    : ICommandHandler<UpdateProjectCommand, ProjectDto>
{
    public async Task<ErrorOr<ProjectDto>> Handle(
        UpdateProjectCommand request,
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

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return Error.Validation("Project.InvalidName", "Project title cannot be empty.");
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        var changed = false;
        if (name != project.Name)
        {
            project.Name = name;
            changed = true;
        }

        if (description != project.Description)
        {
            project.Description = description;
            changed = true;
        }

        if (changed)
        {
            await projectRepository.UpdateAsync(project, cancellationToken);
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
