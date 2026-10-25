using Microsoft.Extensions.Logging;
using Skemex.Application.Events.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Application.Services.Notifications;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Events.Documents;

namespace Skemex.Application.Events.Documents;

public sealed class DocumentVectorizationFinishedNotificationHandler(
    INotificationPublisher notificationPublisher,
    ILogger<DocumentVectorizationFinishedNotificationHandler> logger)
    : IDomainEventHandler<DocumentVectorizationFinishedDomainEvent>
{
    public async Task Handle(
        DocumentVectorizationFinishedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (severity, title, body) = BuildMessage(domainEvent);
            var href = string.IsNullOrWhiteSpace(domainEvent.ProjectCode)
                ? null
                : $"/{domainEvent.ProjectCode.Trim()}/documents/{domainEvent.DocumentId}";

            await notificationPublisher.PublishAsync(
                new NotificationPublishRequest
                {
                    UserId = domainEvent.UserId,
                    Severity = severity,
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
                "Failed to publish vectorization notification for document {DocumentId}.",
                domainEvent.DocumentId);
        }
    }

    private static (NotificationSeverity Severity, string Title, string Body) BuildMessage(
        DocumentVectorizationFinishedDomainEvent domainEvent)
    {
        var fileName = string.IsNullOrWhiteSpace(domainEvent.FileName)
            ? "Document"
            : domainEvent.FileName.Trim();

        return domainEvent.Status switch
        {
            ProjectDocumentVectorizationStatus.Ready => (
                NotificationSeverity.Success,
                "Document indexed",
                domainEvent.ChunkCount is int count and > 0
                    ? $"{fileName} is ready for AI search ({count} chunks)."
                    : $"{fileName} is ready for AI search."),
            ProjectDocumentVectorizationStatus.Skipped => (
                NotificationSeverity.Info,
                "Document indexing skipped",
                string.IsNullOrWhiteSpace(domainEvent.Error)
                    ? $"{fileName} was skipped (unsupported or empty for indexing)."
                    : $"{fileName}: {Truncate(domainEvent.Error!, 400)}"),
            ProjectDocumentVectorizationStatus.Failed => (
                NotificationSeverity.Error,
                "Document indexing failed",
                string.IsNullOrWhiteSpace(domainEvent.Error)
                    ? $"{fileName} could not be indexed."
                    : $"{fileName}: {Truncate(domainEvent.Error!, 400)}"),
            _ => (
                NotificationSeverity.Info,
                "Document indexing finished",
                $"{fileName} indexing finished with status {domainEvent.Status}."),
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
