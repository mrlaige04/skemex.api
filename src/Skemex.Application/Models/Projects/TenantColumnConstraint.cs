namespace Skemex.Application.Models.Projects;

public sealed record TenantColumnConstraint(
    bool IsRequired,
    bool IsSortOrderForced,
    int? ForcedSortOrder);
