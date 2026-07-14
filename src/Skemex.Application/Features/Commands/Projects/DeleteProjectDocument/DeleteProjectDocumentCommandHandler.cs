using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectDocument;

public sealed class DeleteProjectDocumentCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectDocument> documentRepository,
    IProjectDocumentStorageService documentStorage)
    : ICommandHandler<DeleteProjectDocumentCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectDocumentCommand request,
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

        var document = await documentRepository.GetAsync(
            filter: entry =>
                entry.Id == request.DocumentId && entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (document is null)
        {
            return Error.NotFound("ProjectDocument.NotFound", "Document was not found.");
        }

        await documentRepository.DeleteAsync(document, cancellationToken);

        try
        {
            await documentStorage.DeleteAsync(document.BlobId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            /* best-effort blob cleanup */
        }

        return Result.Success;
    }
}
