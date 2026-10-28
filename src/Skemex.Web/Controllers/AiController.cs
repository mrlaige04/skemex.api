using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Web.Attributes;
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
    [Permission(PermissionNames.AiExecute)]
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
    /// Accepts JSON or multipart (files + fields) for chat attachments.
    /// </summary>
    [Permission(PermissionNames.AiExecute)]
    [HttpPost("execute")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Execute(CancellationToken cancellationToken = default)
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

        var command = await BuildEnqueueCommandAsync(tenantId.Value, userId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (command.IsError)
        {
            return Problem(command.Errors);
        }

        var result = await sender.Send(command.Value, cancellationToken);
        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        return AcceptedAtAction(
            nameof(GetExecution),
            new { jobId = result.Value.Id },
            result.Value);
    }

    [Permission(PermissionNames.AiExecute)]
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

    private async Task<ErrorOr<EnqueueAiAgentExecutionCommand>> BuildEnqueueCommandAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var uploads = new List<AiChatAttachmentUpload>();
            foreach (var file in form.Files)
            {
                if (file.Length <= 0)
                {
                    continue;
                }

                uploads.Add(
                    new AiChatAttachmentUpload
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        SizeBytes = file.Length,
                        Content = file.OpenReadStream(),
                    });
            }

            AiExecutionSource? source = null;
            if (Enum.TryParse<AiExecutionSource>(form["source"], ignoreCase: true, out var parsedSource))
            {
                source = parsedSource;
            }

            Guid? projectId = Guid.TryParse(form["projectId"], out var parsedProject)
                ? parsedProject
                : null;
            Guid? chatId = Guid.TryParse(form["chatId"], out var parsedChat)
                ? parsedChat
                : null;

            return new EnqueueAiAgentExecutionCommand
            {
                TenantId = tenantId,
                UserId = userId,
                ProjectId = projectId,
                ChatId = chatId,
                ToolName = NullIfWhiteSpace(form["toolName"]),
                UserInput = form["userInput"].ToString() ?? string.Empty,
                ArgumentsJson = NullIfWhiteSpace(form["argumentsJson"]),
                Model = NullIfWhiteSpace(form["model"]),
                Source = source,
                Attachments = uploads,
                Artefacts = ParseArtefactsJson(form["artefactsJson"]),
            };
        }

        var body = await HttpContext.Request.ReadFromJsonAsync<ExecuteAiRequest>(cancellationToken)
            .ConfigureAwait(false)
            ?? new ExecuteAiRequest();

        return new EnqueueAiAgentExecutionCommand
        {
            TenantId = tenantId,
            UserId = userId,
            ProjectId = body.ProjectId,
            ChatId = body.ChatId,
            ToolName = body.ToolName,
            UserInput = body.UserInput ?? string.Empty,
            ArgumentsJson = body.ArgumentsJson,
            Model = body.Model,
            Source = body.Source,
            Artefacts = body.Artefacts,
        };
    }

    private static IReadOnlyList<AiChatArtefactRef>? ParseArtefactsJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<AiChatArtefactRef>>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
