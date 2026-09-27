namespace Skemex.Application.Models.Ai;

/// <summary>Extensible artifact returned by the project_qa tool.</summary>
public sealed class ProjectQaAnswerArtifact
{
    public const string ArtifactType = "ProjectQaAnswer";

    public required string AnswerMarkdown { get; init; }

    public IReadOnlyList<ProjectQaReference> References { get; init; } = [];
}

public sealed class ProjectQaReference
{
    /// <summary><c>Document</c> or <c>Task</c> (extensible for future source types).</summary>
    public required string Type { get; init; }

    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Optional short excerpt for UI previews / hover cards.</summary>
    public string? Preview { get; init; }
}
