using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Ai;

/// <summary>Catalog entry for an LLM offered by a provider (e.g. Groq). Global, not tenant-scoped.</summary>
public class AiModel : BaseEntity
{
    /// <summary>Provider key, e.g. <c>groq</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider display name snapshot, e.g. <c>Groq</c>.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Provider's model id, e.g. <c>llama-3.1-8b-instant</c>.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Model author / owner from the vendor catalog (e.g. Groq <c>owned_by</c>).</summary>
    public string? Author { get; set; }

    /// <summary>UI icon hint from the provider when available; otherwise null.</summary>
    public string? IconKey { get; set; }

    public bool IsActive { get; set; } = true;
}
