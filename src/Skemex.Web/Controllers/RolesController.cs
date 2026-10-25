using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Roles.CreateTenantRole;
using Skemex.Application.Features.Commands.Roles.DeleteTenantRole;
using Skemex.Application.Features.Commands.Roles.UpdateTenantRole;
using Skemex.Application.Features.Queries.Roles.GetPermissionCatalog;
using Skemex.Application.Features.Queries.Roles.GetTenantRolesDetailed;
using Skemex.Domain.Consts;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

[Route("api/roles")]
[Authorize]
public sealed class RolesController(ISender sender) : BaseController
{
    [Permission(PermissionNames.SettingsRolesView)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantRolesDetailedQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsRolesView)]
    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPermissionCatalogQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsRolesCreate)]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsRolesEdit)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTenantRoleCommand command,
        CancellationToken cancellationToken)
    {
        command.RoleId = id;
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsRolesDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteTenantRoleCommand { RoleId = id }, cancellationToken);
        return result.Match(_ => NoContent(), Problem);
    }
}
