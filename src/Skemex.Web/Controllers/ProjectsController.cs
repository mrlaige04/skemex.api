using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Projects.CreateProject;
using Skemex.Application.Features.Commands.Projects.CreateProjectColumn;
using Skemex.Application.Features.Commands.Projects.CreateProjectTask;
using Skemex.Application.Features.Commands.Projects.DeleteProject;
using Skemex.Application.Features.Commands.Projects.DeleteProjectColumn;
using Skemex.Application.Features.Commands.Projects.DeleteProjectTask;
using Skemex.Application.Features.Commands.Projects.UpdateProjectTask;
using Skemex.Application.Features.Commands.Projects.ReorderProjectColumns;
using Skemex.Application.Features.Commands.Projects.UpdateProjectColumn;
using Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;
using Skemex.Application.Features.Queries.Projects.GetAvailableProjectColumns;
using Skemex.Application.Features.Queries.Projects.GetProjectById;
using Skemex.Application.Features.Queries.Projects.GetProjectColumns;
using Skemex.Application.Features.Queries.Projects.GetProjectTasksByColumnId;
using Skemex.Application.Features.Queries.Projects.GetProjectSettings;
using Skemex.Application.Features.Queries.Projects.GetProjectUsers;
using Skemex.Application.Features.Queries.Projects.GetProjects;
using Skemex.Web.Models.Projects;

namespace Skemex.Web.Controllers;

[Route("api/projects")]
[Authorize]
public class ProjectsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProjectsQuery { Search = search, PageNumber = page, PageSize = pageSize },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}/columns/available")]
    public async Task<IActionResult> ListAvailableColumns(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAvailableProjectColumnsQuery { ProjectId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("{id:guid}/columns")]
    public async Task<IActionResult> CreateColumn(
        Guid id,
        [FromBody] CreateProjectColumnRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateProjectColumnCommand
            {
                ProjectId = id,
                TenantColumnId = body.TenantColumnId,
                Key = body.Key,
                Title = body.Title,
                Description = body.Description,
            },
            cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(ListColumns), new { id }, dto), Problem);
    }

    [HttpPatch("{id:guid}/columns/{columnId:guid}")]
    public async Task<IActionResult> PatchColumn(
        Guid id,
        Guid columnId,
        [FromBody] UpdateProjectColumnRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProjectColumnCommand
            {
                ProjectId = id,
                ColumnId = columnId,
                Title = body.Title,
                Description = body.Description,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPut("{id:guid}/columns/reorder")]
    public async Task<IActionResult> ReorderColumns(
        Guid id,
        [FromBody] ReorderProjectColumnsRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReorderProjectColumnsCommand
            {
                ProjectId = id,
                ColumnIds = body.ColumnIds,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}/columns/{columnId:guid}")]
    public async Task<IActionResult> DeleteColumn(
        Guid id,
        Guid columnId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteProjectColumnCommand
            {
                ProjectId = id,
                ColumnId = columnId,
            },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("{id:guid}/columns/{columnId:guid}/tasks")]
    public async Task<IActionResult> ListTasksByColumn(
        Guid id,
        Guid columnId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetProjectTasksByColumnIdQuery
            {
                ProjectId = id,
                ColumnId = columnId,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> CreateTask(
        Guid id,
        [FromBody] CreateProjectTaskRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateProjectTaskCommand
            {
                ProjectId = id,
                Title = body.Title,
                Description = body.Description,
                AssigneeId = body.AssigneeId,
                ParentId = body.ParentId,
            },
            cancellationToken);
        return result.Match(
            dto => CreatedAtAction(
                nameof(ListTasksByColumn),
                new { id, columnId = dto.ProjectColumnId },
                dto),
            Problem);
    }

    [HttpGet("{id:guid}/settings")]
    public async Task<IActionResult> GetSettings(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetProjectSettingsQuery { ProjectId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("{id:guid}/settings")]
    public async Task<IActionResult> PatchSettings(
        Guid id,
        [FromBody] UpdateProjectSettingsRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProjectSettingsCommand
            {
                ProjectId = id,
                DefaultTaskColumnId = body.DefaultTaskColumnId,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> PatchTask(
        Guid id,
        Guid taskId,
        [FromBody] UpdateProjectTaskRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProjectTaskCommand
            {
                ProjectId = id,
                TaskId = taskId,
                ColumnId = body.ColumnId,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}/columns/{columnId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(
        Guid id,
        Guid columnId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteProjectTaskCommand
            {
                ProjectId = id,
                ColumnId = columnId,
                TaskId = taskId,
            },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpGet("{id:guid}/columns")]
    public async Task<IActionResult> ListColumns(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetProjectColumnsQuery { ProjectId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}/users")]
    public async Task<IActionResult> ListUsers(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProjectUsersQuery
            {
                ProjectId = id,
                Search = search,
                PageNumber = page,
                PageSize = pageSize,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProjectByIdQuery { ProjectId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreateProjectForm form,
        CancellationToken cancellationToken)
    {
        var command = new CreateProjectCommand
        {
            Name = form.Name,
            Code = form.Code,
            Description = form.Description,
        };

        if (form.Logo is { Length: > 0 })
        {
            var ms = new MemoryStream();
            await form.Logo.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            command.LogoImage = ms;
            command.LogoImageContentType = form.Logo.ContentType;
            command.LogoImageFileName = form.Logo.FileName;
        }

        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectCommand { ProjectId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}
