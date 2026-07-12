using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.TenantColumns.CreateTenantColumn;
using Skemex.Application.Features.Commands.TenantColumns.DeleteTenantColumn;
using Skemex.Application.Features.Commands.TenantColumns.ReorderTenantColumns;
using Skemex.Application.Features.Commands.TenantColumns.UpdateTenantColumn;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumnById;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Web.Models.TenantColumns;

namespace Skemex.Web.Controllers;

[Route("api/tenant-columns")]
[Authorize]
public class TenantColumnsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantColumnsQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantColumnByIdQuery { ColumnId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantColumnCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateTenantColumnRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTenantColumnCommand
        {
            ColumnId = id,
            Title = body.Title,
            Description = body.Description,
            IsRequired = body.IsRequired,
            IsSortOrderForced = body.IsSortOrderForced,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(
        [FromBody] ReorderTenantColumnsRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReorderTenantColumnsCommand { ColumnIds = body.ColumnIds },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteTenantColumnCommand { ColumnId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}
