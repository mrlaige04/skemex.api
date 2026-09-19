namespace Skemex.Application.Models.Ai;

public sealed class AiAssignmentContext
{
    public IReadOnlyList<AiSpecializationDefinition> SpecializationDefinitions { get; init; } = [];
    public IReadOnlyList<AiAvailableMember> AvailableMembers { get; init; } = [];

    public IReadOnlySet<Guid> ValidAssigneeIds { get; init; } = new HashSet<Guid>();
}

public sealed class AiSpecializationDefinition
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AiAvailableMember
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> SpecializationTitles { get; init; } = [];
    public IReadOnlyList<string> Skills { get; init; } = [];
    public int ActiveTasksCount { get; init; }
}
