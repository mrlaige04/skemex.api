using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectDocuments;

public sealed class GetProjectDocumentsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectDocument> documentRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectDocumentsQuery, PaginatedList<ProjectDocumentDto>>
{
    public async Task<ErrorOr<PaginatedList<ProjectDocumentDto>>> Handle(
        GetProjectDocumentsQuery request,
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

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<ProjectDocument, bool>> filter = document => document.ProjectId == request.ProjectId;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = document =>
                document.ProjectId == request.ProjectId &&
                document.FileName.ToLower().Contains(term);
        }

        var paginated = await documentRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: query => query
                .Include(document => document.UploadedBy)
                .OrderByDescending(document => document.CreatedAt)
                .ThenBy(document => document.FileName),
            cancellationToken: cancellationToken);

        var urlTasks = paginated.Items.Select(async document =>
        {
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
                DownloadUrl = downloadUrl,
                UploadedBy = new ProjectDocumentUserDto
                {
                    Id = document.UploadedBy.Id,
                    FirstName = document.UploadedBy.FirstName,
                    LastName = document.UploadedBy.LastName,
                    Email = document.UploadedBy.Email ?? string.Empty,
                },
            };
        });

        var items = (await Task.WhenAll(urlTasks).ConfigureAwait(false)).ToList();

        return new PaginatedList<ProjectDocumentDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }
}
