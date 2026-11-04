namespace Skemex.Domain.Enums;

/// <summary>Directed link type between two project tasks (excludes parent/sub-task hierarchy).</summary>
public enum TaskRelationType
{
    /// <summary>Source task is blocked by the target task.</summary>
    BlockedBy = 1,

    /// <summary>Source task is related to the target task.</summary>
    RelatesTo = 2,

    /// <summary>Source task duplicates the target task.</summary>
    Duplicates = 3,
}
