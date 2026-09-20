using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Web.Models.Ai;

namespace Skemex.Web.Controllers;

[Authorize]
[Route("api/ai")]
public sealed class AiController(
    IAiService aiService,
    ICurrentUser currentUser,
    ISender sender,
    ITenantRepository<AiAgentJob> agentJobRepository) : BaseController
{
    /// <summary>Lists active models from the local catalog (read-only; no sync).</summary>
    [HttpGet("models")]
    public async Task<IActionResult> ListModels(
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var models = await aiService.ListModelsAsync(refresh, cancellationToken);
        return Ok(models);
    }

    /// <summary>
    /// Unified agent entry: schedules a background job.
    /// Provide <c>toolName</c> for direct tool execution; omit it for chat + function calling.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] ExecuteAiRequest? body,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        body ??= new ExecuteAiRequest();
        var result = await sender.Send(
            new EnqueueAiAgentExecutionCommand
            {
                TenantId = tenantId.Value,
                UserId = userId.Value,
                ProjectId = body.ProjectId,
                ChatId = body.ChatId,
                ToolName = body.ToolName,
                UserInput = body.UserInput ?? string.Empty,
                CustomInstructions = body.CustomInstructions,
                ArgumentsJson = body.ArgumentsJson,
                Model = body.Model,
            },
            cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        return AcceptedAtAction(
            nameof(GetExecution),
            new { jobId = result.Value.Id },
            result.Value);
    }

    [HttpGet("execute/{jobId:guid}")]
    public async Task<IActionResult> GetExecution(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await agentJobRepository.GetAsync(
            filter: entry => entry.Id == jobId,
            cancellationToken: cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(AiAgentJobDto.FromEntity(job));
    }
}
