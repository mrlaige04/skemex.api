using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectDocumentById;

public sealed class GetProjectDocumentByIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectDocument> documentRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectDocumentByIdQuery, ProjectDocumentDto>
{
    public async Task<ErrorOr<ProjectDocumentDto>> Handle(
        GetProjectDocumentByIdQuery request,
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
            include: query => query.Include(entry => entry.UploadedBy),
            cancellationToken: cancellationToken);
        if (document is null)
        {
            return Error.NotFound("ProjectDocument.NotFound", "Document was not found.");
        }

        var downloadUrl = await urlService
            .GetProjectDocumentUrlAsync(document.BlobId, cancellationToken)
            .ConfigureAwait(false);

        return new ProjectDocumentDto
        {
            Id = document.Id,
            ProjectId = document.ProjectId,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            CreatedAt = document.CreatedAt,
            VectorizationStatus = document.VectorizationStatus.ToString(),
            VectorizationError = document.VectorizationError,
            VectorizedChunkCount = document.VectorizedChunkCount,
            DownloadUrl = downloadUrl,
            UploadedBy = new ProjectDocumentUserDto
            {
                Id = document.UploadedBy.Id,
                FirstName = document.UploadedBy.FirstName,
                LastName = document.UploadedBy.LastName,
                Email = document.UploadedBy.Email ?? string.Empty,
            },
        };
    }
}
