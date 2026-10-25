namespace Skemex.Application.Models.Ai.Tools;

public sealed class ProjectQaReference
{
    /// <summary><c>Document</c> or <c>Task</c> (extensible for future source types).</summary>
    public required string Type { get; init; }

    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>Optional short excerpt for UI previews / hover cards.</summary>
    public string? Preview { get; init; }
}
