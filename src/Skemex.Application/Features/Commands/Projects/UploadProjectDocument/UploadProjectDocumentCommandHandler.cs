using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UploadProjectDocument;

public sealed class UploadProjectDocumentCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectDocument> documentRepository,
    IProjectDocumentStorageService documentStorage,
    IUrlService urlService)
    : ICommandHandler<UploadProjectDocumentCommand, ProjectDocumentDto>
{
    private const long MaxFileBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/png",
        "image/jpeg",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".png", ".jpg", ".jpeg",
    };

    public async Task<ErrorOr<ProjectDocumentDto>> Handle(
        UploadProjectDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCore(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.FileContent?.Dispose();
        }
    }

    private async Task<ErrorOr<ProjectDocumentDto>> HandleCore(
        UploadProjectDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before uploading documents.");
        }

        var project = await projectRepository.GetAsync(
            filter: entry => entry.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var validation = ValidateFile(request);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        var contentType = request.ContentType!.Trim();
        var fileName = Path.GetFileName(request.FileName?.Trim() ?? "document");
        var fileSize = request.FileContent!.Length;

        string blobId;
        try
        {
            blobId = await documentStorage
                .CreateAsync(
                    tenantId.Value,
                    request.ProjectId,
                    request.FileContent,
                    contentType,
                    fileName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Error.Unexpected(
                "ProjectDocument.UploadFailed",
                "Could not upload the document to storage.");
        }

        var document = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            BlobId = blobId,
            UploadedById = userId.Value,
        };

        await documentRepository.AddAsync(document, cancellationToken);

        var created = await documentRepository.GetAsync(
            filter: entry => entry.Id == document.Id,
            include: query => query.Include(entry => entry.UploadedBy),
            cancellationToken: cancellationToken);

        if (created is null)
        {
            return Error.Unexpected(
                "ProjectDocument.CreateFailed",
                "Document was uploaded but could not be loaded.");
        }

        return MapDto(created);
    }

    private static ErrorOr<Success> ValidateFile(UploadProjectDocumentCommand request)
    {
        if (request.FileContent is null || !request.FileContent.CanRead)
        {
            return Error.Validation("ProjectDocument.FileRequired", "A file is required.");
        }

        if (!request.FileContent.CanSeek)
        {
            return Error.Validation("ProjectDocument.InvalidFile", "Uploaded file could not be read.");
        }

        if (request.FileContent.Length == 0)
        {
            return Error.Validation("ProjectDocument.EmptyFile", "Uploaded file is empty.");
        }

        if (request.FileContent.Length > MaxFileBytes)
        {
            return Error.Validation(
                "ProjectDocument.FileTooLarge",
                "File size cannot exceed 25 MB.");
        }

        var contentType = request.ContentType?.Trim();
        if (string.IsNullOrEmpty(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return Error.Validation(
                "ProjectDocument.InvalidContentType",
                "Only PDF, DOCX, PNG, and JPG files are allowed.");
        }

        var extension = Path.GetExtension(request.FileName ?? string.Empty);
        if (!AllowedExtensions.Contains(extension))
        {
            return Error.Validation(
                "ProjectDocument.InvalidExtension",
                "Only .pdf, .docx, .png, and .jpg files are allowed.");
        }

        return Result.Success;
    }

    private ProjectDocumentDto MapDto(ProjectDocument document) =>
        new()
        {
            Id = document.Id,
            ProjectId = document.ProjectId,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            CreatedAt = document.CreatedAt,
            DownloadUrl = urlService.GetProjectDocumentUrl(document.BlobId),
            UploadedBy = new ProjectDocumentUserDto
            {
                Id = document.UploadedBy.Id,
                FirstName = document.UploadedBy.FirstName,
                LastName = document.UploadedBy.LastName,
                Email = document.UploadedBy.Email ?? string.Empty,
            },
        };
}
