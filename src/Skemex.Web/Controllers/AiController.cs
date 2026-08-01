using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Services.Ai;

namespace Skemex.Web.Controllers;

[Authorize]
[Route("api/ai")]
public sealed class AiController(IAiChatService aiChatService) : BaseController
{
    /// <summary>Lists (and syncs from configured providers when stale) available AI models.</summary>
    [HttpGet("models")]
    public async Task<IActionResult> ListModels(
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var models = await aiChatService.ListModelsAsync(refresh, cancellationToken);
        return Ok(models);
    }
}
