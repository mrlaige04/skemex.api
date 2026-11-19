using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Tenants.UpdateTenantSettings;
using Skemex.Application.Features.Queries.Tenants.GetTenantSettings;
using Skemex.Domain.Consts;
using Skemex.Infrastructure.Tenants.CreateTenant;
using Skemex.Infrastructure.Tenants.UpdateCurrentTenant;
using Skemex.Web.Attributes;
using Skemex.Web.Models.Tenants;

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
    [HttpGet("current/settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTenantSettingsQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [Permission(PermissionNames.SettingsWorkspaceEdit)]
    [HttpPatch("current/settings")]
    public async Task<IActionResult> PatchSettings(
        [FromBody] UpdateTenantSettingsRequest body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateTenantSettingsCommand
            {
                DefaultAiModelId = body.DefaultAiModelId,
                ClearDefaultAiModel = body.ClearDefaultAiModel,
                EnrichmentAiModelId = body.EnrichmentAiModelId,
                ClearEnrichmentAiModel = body.ClearEnrichmentAiModel,
                AiTemperature = body.AiTemperature,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }
}
