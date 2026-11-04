namespace Skemex.Application.Models.Projects;

/// <summary>Project-wide task graph: issues as nodes, parent/child and relations as edges.</summary>
public sealed class ProjectTaskGraphDto
{
    public IReadOnlyList<ProjectTaskGraphNodeDto> Nodes { get; init; } = [];
    public IReadOnlyList<ProjectTaskGraphEdgeDto> Edges { get; init; } = [];

    /// <summary>True when the response was capped at the server node limit.</summary>
    public bool Truncated { get; init; }
}

public sealed class ProjectTaskGraphNodeDto
{
    public Guid Id { get; init; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid ProjectColumnId { get; set; }
    public string ColumnKey { get; set; } = string.Empty;
    public string ColumnTitle { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
}

/// <summary>
/// Directed edge. <see cref="Kind"/> is <c>ParentOf</c> (source=parent, target=child)
/// or a <c>TaskRelationType</c> name (<c>BlockedBy</c>, <c>RelatesTo</c>, <c>Duplicates</c>)
/// matching DB semantics (source blocked by / relates to / duplicates target).
/// </summary>
public sealed class ProjectTaskGraphEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public Guid SourceTaskId { get; set; }
    public Guid TargetTaskId { get; set; }
    public string Kind { get; set; } = string.Empty;
}
