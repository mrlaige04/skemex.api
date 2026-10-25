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
