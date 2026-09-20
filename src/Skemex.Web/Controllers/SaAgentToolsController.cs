using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaFeatures.Commands.SaAgentTools.UpdateSaAgentTool;
using Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentToolById;
using Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentTools;
using Skemex.Web.Attributes;
using Skemex.Web.Models.SuperAdmin;

namespace Skemex.Web.Controllers;

[Route("api/sa/agent-tools")]
[Authorize]
[OnlySuperAdmin]
public class SaAgentToolsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSaAgentToolsQuery { Search = search },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaAgentToolByIdQuery { ToolId = id }, cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(
        Guid id,
        [FromBody] UpdateSaAgentToolRequest body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSaAgentToolCommand
        {
            ToolId = id,
            Description = body.Description,
            SystemPrompt = body.SystemPrompt,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(Ok, Problem);
    }
}
