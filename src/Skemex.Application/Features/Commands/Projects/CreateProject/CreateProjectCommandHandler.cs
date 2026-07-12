using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateProject;

public sealed class CreateProjectCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<TenantUser> tenantUserRepository,
    IProjectLogoService projectLogos,
    IUrlService urlService,
    ProjectColumnSeeder projectColumnSeeder)
    : ICommandHandler<CreateProjectCommand, ProjectDto>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    public async Task<ErrorOr<ProjectDto>> Handle(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCore(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.LogoImage?.Dispose();
        }
    }

    private async Task<ErrorOr<ProjectDto>> HandleCore(
        CreateProjectCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var currentUserId = currentUser.GetUserId();
        if (currentUserId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before creating projects.");
        }

        if (request.LogoImage is not null)
        {
            var imageValidation = ValidateLogoImage(request);
            if (imageValidation.IsError)
            {
                return imageValidation.Errors;
            }
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await projectRepository.ExistsAsync(
            p => p.TenantId == tenantId && p.Code == code,
            cancellationToken: cancellationToken);
        if (exists)
        {
            return Error.Conflict("Project.CodeAlreadyExists", "Project code already exists in this company.");
        }

        var requestedUserIds = request.UserIds.Count > 0
            ? request.UserIds.Distinct().ToList()
            : [currentUserId.Value];

        var tenantUsers = await tenantUserRepository.GetAllAsync(
            filter: tu => tu.TenantId == tenantId && requestedUserIds.Contains(tu.UserId),
            include: q => q.Include(tu => tu.User),
            cancellationToken: cancellationToken);

        if (tenantUsers.Count != requestedUserIds.Count)
        {
            return Error.Validation("Project.UsersInvalid", "One or more users are not members of this company.");
        }

        var projectId = Guid.NewGuid();
        string? logoBlobId = null;

        if (request.LogoImage is not null)
        {
            request.LogoImage.Position = 0;
            logoBlobId = await projectLogos.CreateAsync(
                    tenantId.Value,
                    projectId,
                    request.LogoImage,
                    request.LogoImageContentType ?? "application/octet-stream",
                    request.LogoImageFileName,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var projectColumns = await projectColumnSeeder.CreateForProjectAsync(
            tenantId.Value,
            projectId,
            cancellationToken);

        var defaultColumn = projectColumns
            .OrderBy(column => column.SortOrder)
            .ThenBy(column => column.Title)
            .First();

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId.Value,
            Name = request.Name.Trim(),
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            LogoBlobId = logoBlobId,
            Users = tenantUsers.Select(tu => new ProjectUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                UserId = tu.UserId,
                User = tu.User,
            }).ToList(),
            Columns = projectColumns.ToList(),
            TaskCounter = new ProjectTaskCounter
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                ProjectId = projectId,
                NextNumber = 1,
            },
            Settings = new ProjectSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                ProjectId = projectId,
                DefaultTaskColumnId = defaultColumn.Id,
            },
        };

        await projectRepository.AddAsync(project, cancellationToken);

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

    private static ErrorOr<Success> ValidateLogoImage(CreateProjectCommand request)
    {
        if (!AllowedContentTypes.Contains(request.LogoImageContentType ?? string.Empty))
        {
            return Error.Validation(
                "Project.InvalidLogoType",
                "Logo must be JPEG, PNG, WebP, or GIF.");
        }

        if (request.LogoImage is not MemoryStream && request.LogoImage!.CanSeek is false)
        {
            return Error.Validation("Project.LogoNotReadable", "Could not read the uploaded logo.");
        }

        try
        {
            if (request.LogoImage!.Length > MaxImageBytes)
            {
                return Error.Validation("Project.LogoTooLarge", "Logo must be at most 5 MB.");
            }
        }
        catch (NotSupportedException)
        {
            return Error.Validation("Project.LogoNotReadable", "Could not read the uploaded logo.");
        }

        return Result.Success;
    }
}
