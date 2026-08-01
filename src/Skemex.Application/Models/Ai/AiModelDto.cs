namespace Skemex.Application.Models.Ai;

public sealed class AiModelDto
{
    public Guid Id { get; init; }

    /// <summary>Provider key (immutable slug stored on the model row).</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>Provider display name for UI grouping.</summary>
    public string ProviderName { get; init; } = string.Empty;

    public string ExternalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? IconKey { get; init; }
    public bool IsActive { get; init; }

    public static AiModelDto FromEntity(
        Domain.Entities.Ai.AiModel model,
        string? providerName = null)
    {
        var resolvedName = FirstNonEmpty(
            providerName,
            model.ProviderName,
            model.Provider);

        return new AiModelDto
        {
            Id = model.Id,
            Provider = model.Provider,
            ProviderName = resolvedName,
            ExternalId = model.ExternalId,
            DisplayName = model.DisplayName,
            Author = model.Author,
            IconKey = model.IconKey,
            IsActive = model.IsActive,
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
