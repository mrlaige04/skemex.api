using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Services;
using Skemex.Infrastructure.Services.Ai.Tools;

namespace Skemex.Infrastructure.Services.Ai;

/// <summary>Hangfire entry for legacy decomposition jobs; delegates work to <see cref="TaskDecompositionTool"/>.</summary>
public sealed class AiTaskDecompositionService(
    ITenantRepository<AiDecompositionJob> jobRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    IBaseRepository<AgentTool> agentToolRepository,
    TaskDecompositionTool decompositionTool,
    ILogger<AiTaskDecompositionService> logger) : IAiTaskDecompositionService
{
    public string Enqueue(Guid jobId, Guid tenantId, Guid requestedByUserId) =>
        BackgroundJob.Enqueue<AiTaskDecompositionService>(service =>
            service.ProcessJobAsync(jobId, tenantId, requestedByUserId, CancellationToken.None));

    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessJobAsync(
        Guid jobId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var ambient = AmbientUserContext.Use(tenantId, requestedByUserId);

        var job = await jobRepository.GetAsync(
            filter: entry => entry.Id == jobId,
            cancellationToken: cancellationToken);

        if (job is null)
        {
            logger.LogWarning("AI decomposition job {JobId} was not found.", jobId);
            return;
        }

        if (job.Status is AiDecompositionJobStatus.Succeeded or AiDecompositionJobStatus.Failed)
        {
            logger.LogInformation(
                "AI decomposition job {JobId} already finished with status {Status}.",
                jobId,
                job.Status);
            return;
        }

        job.Status = AiDecompositionJobStatus.Running;
        job.Error = null;
        await jobRepository.UpdateAsync(job, cancellationToken);

        try
        {
            var dbTool = await agentToolRepository.GetAsync(
                filter: entry => entry.SystemName == TaskDecompositionToolDefaults.SystemName,
                cancellationToken: cancellationToken);

            var context = new AgentExecutionContext
            {
                TenantId = tenantId,
                UserId = requestedByUserId,
                ProjectId = job.ProjectId,
                ChatId = job.AiChatId,
                DecompositionJobId = job.Id,
                Model = null,
                EffectiveDescription = !string.IsNullOrWhiteSpace(dbTool?.Description)
                    ? dbTool.Description
                    : decompositionTool.DefaultDescription,
                EffectiveSystemPrompt = !string.IsNullOrWhiteSpace(dbTool?.SystemPrompt)
                    ? dbTool.SystemPrompt
                    : decompositionTool.DefaultSystemPrompt,
            };

            var args = JsonSerializer.SerializeToElement(new
            {
                userInput = job.UserInput,
                customInstructions = job.CustomInstructions,
                projectId = job.ProjectId.ToString(),
            });

            var result = await decompositionTool
                .ExecuteDirectAsync(args, context, cancellationToken)
                .ConfigureAwait(false);

            // Reload — tool may have updated RootTaskId / Succeeded.
            job = await jobRepository.GetAsync(
                filter: entry => entry.Id == jobId,
                cancellationToken: cancellationToken) ?? job;

            if (!result.Success)
            {
                job.Status = AiDecompositionJobStatus.Failed;
                job.Error = Truncate(result.ErrorMessage ?? "Decomposition failed.", 2000);
                await jobRepository.UpdateAsync(job, cancellationToken);
                await AppendAssistantMessageAsync(
                    job,
                    content: result.AssistantMessage ?? $"Decomposition failed: {job.Error}",
                    rootTaskId: null,
                    cancellationToken);
                return;
            }

            if (job.Status != AiDecompositionJobStatus.Succeeded)
            {
                job.Status = AiDecompositionJobStatus.Succeeded;
                job.Error = null;
                await jobRepository.UpdateAsync(job, cancellationToken);
            }

            await AppendAssistantMessageAsync(
                job,
                content: result.AssistantMessage
                    ?? "Done — the task tree was created. Refresh the board or Issues to see the new tasks.",
                rootTaskId: job.RootTaskId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI decomposition job {JobId} failed.", jobId);
            job.Status = AiDecompositionJobStatus.Failed;
            job.Error = Truncate(ex.Message, 2000);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                content: $"Decomposition failed: {job.Error}",
                rootTaskId: null,
                cancellationToken);
            throw;
        }
    }

    private async Task AppendAssistantMessageAsync(
        AiDecompositionJob job,
        string content,
        Guid? rootTaskId,
        CancellationToken cancellationToken)
    {
        if (job.AiChatId is null)
        {
            return;
        }

        var chat = await chatRepository.GetAsync(
            filter: entry => entry.Id == job.AiChatId.Value,
            cancellationToken: cancellationToken);
        if (chat is null)
        {
            logger.LogWarning(
                "AI chat {ChatId} was not found while appending decomposition result for job {JobId}.",
                job.AiChatId,
                job.Id);
            return;
        }

        await messageRepository.AddAsync(
            new AiChatMessage
            {
                Id = Guid.NewGuid(),
                TenantId = job.TenantId,
                ChatId = chat.Id,
                Role = AiChatMessageRole.Assistant,
                Content = content,
                DecompositionJobId = job.Id,
                RootTaskId = rootTaskId,
            },
            cancellationToken);

        chat.UpdatedAt = DateTime.UtcNow;
        await chatRepository.UpdateAsync(chat, cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
