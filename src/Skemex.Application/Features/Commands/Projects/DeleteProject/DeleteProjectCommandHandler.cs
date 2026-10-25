using ErrorOr;
using Skemex.Application.Features.Abstractions;
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

namespace Skemex.Application.Features.Commands.Projects.DeleteProject;

public sealed class DeleteProjectCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    IProjectLogoService projectLogos)
    : ICommandHandler<DeleteProjectCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectCommand request,
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

        var logoBlobId = project.LogoBlobId;
        await projectRepository.DeleteAsync(project, cancellationToken);

        if (!string.IsNullOrWhiteSpace(logoBlobId))
        {
            try
            {
                await projectLogos.DeleteAsync(logoBlobId, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                /* best-effort cleanup */
            }
        }

        return Result.Success;
    }
}
