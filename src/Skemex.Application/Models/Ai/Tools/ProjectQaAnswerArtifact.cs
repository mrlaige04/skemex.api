namespace Skemex.Application.Models.Ai.Tools;

/// <summary>Extensible artifact returned by the project_qa tool.</summary>
public sealed class ProjectQaAnswerArtifact
{
    public const string ArtifactType = "ProjectQaAnswer";

    public required string AnswerMarkdown { get; init; }

    public IReadOnlyList<ProjectQaReference> References { get; init; } = [];
}
