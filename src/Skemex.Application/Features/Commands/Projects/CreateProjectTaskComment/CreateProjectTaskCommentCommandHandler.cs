using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Common;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTaskComment;

public sealed class CreateProjectTaskCommentCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskComment> commentRepository,
    IUrlService urlService)
    : ICommandHandler<CreateProjectTaskCommentCommand, ProjectTaskCommentDto>
{
    public async Task<ErrorOr<ProjectTaskCommentDto>> Handle(
        CreateProjectTaskCommentCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null || userId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var bodyResult = TryNormalizeBody(request.Body, out var body);
        if (bodyResult.IsError)
        {
            return bodyResult.Errors;
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var taskExists = await projectTaskRepository.ExistsAsync(
            filter: task => task.Id == request.TaskId && task.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!taskExists)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var entry = new ProjectTaskComment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            AuthorId = userId.Value,
            Body = body,
        };

        await commentRepository.AddAsync(entry, cancellationToken);

        var created = await commentRepository.GetAsync(
            filter: comment => comment.Id == entry.Id,
            include: query => query.Include(comment => comment.Author),
            cancellationToken: cancellationToken);

        if (created is null)
        {
            return Error.Unexpected(
                "Comment.CreateFailed",
                "Comment was created but could not be loaded.");
        }

        var avatars = await ProjectTaskCommentDtoHelper
            .LoadAvatarUrlsAsync([created], urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskCommentDtoHelper.Map(created, avatars);
    }

    private static ErrorOr<Success> TryNormalizeBody(string? body, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(body))
        {
            return Error.Validation("Comment.BodyRequired", "Comment cannot be empty.");
        }

        var trimmed = body.Trim();
        if (trimmed.Length > ProjectTaskCommentDtoHelper.MaxBodyLength)
        {
            return Error.Validation(
                "Comment.BodyTooLong",
                $"Comment cannot exceed {ProjectTaskCommentDtoHelper.MaxBodyLength} characters.");
        }

        // Reject empty Quill shells like <p><br></p>.
        var plain = StripTags(trimmed);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return Error.Validation("Comment.BodyRequired", "Comment cannot be empty.");
        }

        normalized = trimmed;
        return Result.Success;
    }

    private static string StripTags(string html)
    {
        var chars = new char[html.Length];
        var n = 0;
        var inside = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inside = true;
                continue;
            }

            if (ch == '>')
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                chars[n++] = ch;
            }
        }

        return new string(chars, 0, n)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
