using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Commands.CreateSaTenant;
using Skemex.Application.SaFeatures.Commands.DeleteSaTenant;
using Skemex.Application.SaFeatures.Commands.UpdateSaTenant;
using Skemex.Application.SaFeatures.Queries.GetSaTenantById;
using Skemex.Application.SaFeatures.Queries.GetSaTenants;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

[Route("api/sa/tenants")]
[Authorize]
[OnlySuperAdmin]
public class SaTenantsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetSaTenantsQuery { Search = search, PageNumber = page, PageSize = pageSize },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaTenantByIdQuery { TenantId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSaTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateSaTenantRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSaTenantCommand
        {
            TenantId = id,
            Name = body.Name,
            Email = body.Email,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSaTenantCommand { TenantId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}

public sealed class UpdateSaTenantRequest
{
    public string? Name { get; set; }

    public string? Email { get; set; }
}
