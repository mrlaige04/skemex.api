using Skemex.Domain.Events.Abstractions;

namespace Skemex.Domain.Events.Projects;

/// <summary>Raised when board-visible project tasks are created, updated, or deleted.</summary>
public sealed class ProjectBoardChangedDomainEvent : IDomainEvent
{
    public required Guid ProjectId { get; init; }
    public Guid? TaskId { get; init; }

    /// <summary>Created | Updated | Deleted</summary>
    public required string ChangeKind { get; init; }
}
