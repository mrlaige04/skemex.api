namespace Skemex.Application.Models.Projects;

public sealed class ProjectColumnDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public bool IsRequired { get; init; }
    public bool IsSortOrderForced { get; init; }
    public int? ForcedSortOrder { get; init; }
}
