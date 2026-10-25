namespace Skemex.Application.Models.Ai.Tools;

public sealed class AiAssignmentContext
{
    public IReadOnlyList<AiSpecializationDefinition> SpecializationDefinitions { get; init; } = [];
    public IReadOnlyList<AiAvailableMember> AvailableMembers { get; init; } = [];

    public IReadOnlySet<Guid> ValidAssigneeIds { get; init; } = new HashSet<Guid>();
}
