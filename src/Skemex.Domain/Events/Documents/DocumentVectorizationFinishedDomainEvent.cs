using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Events.Abstractions;

namespace Skemex.Domain.Events.Documents;

/// <summary>Raised when document vectorization reaches a terminal status.</summary>
public sealed class DocumentVectorizationFinishedDomainEvent : IDomainEvent
{
    public required Guid DocumentId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public string? ProjectCode { get; init; }
    public required string FileName { get; init; }
    public required ProjectDocumentVectorizationStatus Status { get; init; }
    public string? Error { get; init; }
    public int? ChunkCount { get; init; }
}
