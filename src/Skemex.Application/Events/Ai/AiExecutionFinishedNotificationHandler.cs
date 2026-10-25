using Microsoft.Extensions.Logging;
using Skemex.Application.Events.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Application.Services.Notifications;
using Skemex.Domain.Enums;
using Skemex.Domain.Events.Ai;

namespace Skemex.Application.Events.Ai;

public sealed class AiExecutionFinishedNotificationHandler(
    INotificationPublisher notificationPublisher,
    ILogger<AiExecutionFinishedNotificationHandler> logger)
    : IDomainEventHandler<AiExecutionFinishedDomainEvent>
{
    public async Task Handle(
        AiExecutionFinishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        if (domainEvent.Status != AiExecutionStatus.Succeeded)
        {
            return;
        }

        try
        {
            var toolLabel = ResolveToolDisplayName(domainEvent.ToolName ?? domainEvent.ArtifactType);
            var title = string.IsNullOrWhiteSpace(toolLabel)
                ? "AI task completed"
                : $"{toolLabel} completed";

            var body = Truncate(
                !string.IsNullOrWhiteSpace(domainEvent.AssistantMessage)
                    ? domainEvent.AssistantMessage!
                    : !string.IsNullOrWhiteSpace(domainEvent.UserInput)
                        ? domainEvent.UserInput!
                        : "Your AI request finished successfully.",
                500);

            var href = string.IsNullOrWhiteSpace(domainEvent.ProjectCode)
                ? null
                : $"/{domainEvent.ProjectCode.Trim()}/ai-usage";

            await notificationPublisher.PublishAsync(
                new NotificationPublishRequest
                {
                    UserId = domainEvent.UserId,
                    Severity = NotificationSeverity.Success,
                    Title = title,
                    Body = body,
                    Href = href,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to publish success notification for AI execution {ExecutionId} (job {JobId}).",
                domainEvent.AiExecutionId,
                domainEvent.AiAgentJobId);
        }
    }

    private static string ResolveToolDisplayName(string? toolOrArtifact)
    {
        if (string.IsNullOrWhiteSpace(toolOrArtifact))
        {
            return string.Empty;
        }

        return toolOrArtifact.Trim().ToLowerInvariant() switch
        {
            "task_decomposition" or "taskdecomposition" => "Task decomposition",
            "project_qa" or "projectqa" => "Project Q&A",
            "chat" => "AI chat",
            "error" => "AI task",
            _ => HumanizeIdentifier(toolOrArtifact.Trim()),
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        var spaced = value.Replace('_', ' ').Replace('-', ' ').Trim();
        if (spaced.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
