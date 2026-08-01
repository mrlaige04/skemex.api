namespace Skemex.Application.SaModels.SaAiProviders;

public sealed class SaAiProviderModelDto
{
    public Guid Id { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public string ExternalId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Author { get; init; }

    public string? IconKey { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static SaAiProviderModelDto FromEntity(Domain.Entities.Ai.AiModel model) =>
        new()
        {
            Id = model.Id,
            Provider = model.Provider,
            ProviderName = model.ProviderName,
            ExternalId = model.ExternalId,
            DisplayName = model.DisplayName,
            Author = model.Author,
            IconKey = model.IconKey,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
}
