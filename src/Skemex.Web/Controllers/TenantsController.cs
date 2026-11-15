using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Consts;
using Skemex.Infrastructure.Tenants.CreateTenant;
using Skemex.Infrastructure.Tenants.GetTenantAiSettings;
using Skemex.Infrastructure.Tenants.UpdateCurrentTenant;
using Skemex.Infrastructure.Tenants.UpdateTenantAiSettings;
using Skemex.Web.Attributes;

namespace Skemex.Web.Controllers;

[Route("api/tenants")]
[Authorize]
public class TenantsController(ISender sender) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsWorkspaceEdit)]
    [HttpPatch("current")]
    public async Task<IActionResult> UpdateCurrent(
        [FromBody] UpdateCurrentTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsWorkspaceView)]
    [HttpGet("current/ai-settings")]
    public async Task<IActionResult> GetAiSettings(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantAiSettingsQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsWorkspaceEdit)]
    [HttpPatch("current/ai-settings")]
    public async Task<IActionResult> UpdateAiSettings(
        [FromBody] UpdateTenantAiSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }
}
