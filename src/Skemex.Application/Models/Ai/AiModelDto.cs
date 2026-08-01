namespace Skemex.Application.Models.Ai;

public sealed class AiModelDto
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? IconKey { get; init; }
    public bool IsActive { get; init; }

    public static AiModelDto FromEntity(Domain.Entities.Ai.AiModel model) =>
        new()
        {
            Id = model.Id,
            Provider = model.Provider,
            ExternalId = model.ExternalId,
            DisplayName = model.DisplayName,
            IconKey = model.IconKey,
            IsActive = model.IsActive,
        };
}
