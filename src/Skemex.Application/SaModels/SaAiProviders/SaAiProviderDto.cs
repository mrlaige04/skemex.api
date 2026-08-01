namespace Skemex.Application.SaModels.SaAiProviders;

public sealed class SaAiProviderDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public bool HasApiKey { get; init; }

    public IReadOnlyList<SaAiProviderAuthEntryDto> AuthEntries { get; init; } = [];

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed class SaAiProviderAuthEntryDto
{
    public string Type { get; init; } = "header";

    public string Name { get; init; } = string.Empty;

    public bool HasValue { get; init; }
}

public sealed class SaAiProviderAuthEntryInput
{
    public string Type { get; set; } = "header";

    public string Name { get; set; } = string.Empty;

    /// <summary>Plaintext secret. Null/blank on update keeps the existing value.</summary>
    public string? Value { get; set; }
}
