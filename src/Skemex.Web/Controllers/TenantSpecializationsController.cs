using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.TenantSpecializations.AssignTenantSpecialization;
using Skemex.Application.Features.Commands.TenantSpecializations.CreateTenantSpecialization;
using Skemex.Application.Features.Commands.TenantSpecializations.DeleteTenantSpecialization;
using Skemex.Application.Features.Commands.TenantSpecializations.UnassignTenantSpecialization;
using Skemex.Application.Features.Commands.TenantSpecializations.UpdateTenantSpecialization;
using Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializationById;
using Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;
using Skemex.Web.Models.TenantSpecializations;

namespace Skemex.Web.Controllers;

[Route("api/tenant-specializations")]
[Authorize]
public class TenantSpecializationsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantSpecializationsQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetTenantSpecializationByIdQuery { SpecializationId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantSpecializationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(dto => CreatedAtAction(nameof(Get), new { id = dto.Id }, dto), Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateTenantSpecializationRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateTenantSpecializationCommand
            {
                SpecializationId = id,
                Title = body.Title,
                Description = body.Description,
                DefaultSkills = body.DefaultSkills,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new DeleteTenantSpecializationCommand { SpecializationId = id },
            cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("{id:guid}/users/{userId:guid}")]
    public async Task<IActionResult> Assign(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignTenantSpecializationCommand
            {
                SpecializationId = id,
                UserId = userId,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpDelete("{id:guid}/users/{userId:guid}")]
    public async Task<IActionResult> Unassign(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UnassignTenantSpecializationCommand
            {
                SpecializationId = id,
                UserId = userId,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }
}
