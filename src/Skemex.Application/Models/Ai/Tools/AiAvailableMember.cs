namespace Skemex.Application.Models.Ai.Tools;

public sealed class AiAvailableMember
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> SpecializationTitles { get; init; } = [];
    public IReadOnlyList<string> Skills { get; init; } = [];
    public int ActiveTasksCount { get; init; }
}
