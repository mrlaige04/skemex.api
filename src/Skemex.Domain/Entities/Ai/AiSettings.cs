using Skemex.Domain.Consts;

namespace Skemex.Domain.Entities.Ai;

/// <summary>
/// AI configuration owned by <c>ProjectSettings</c> / <c>TenantSettings</c>
/// (columns live on the owner row; not a separate table).
/// </summary>
public class AiSettings
{
    /// <summary>Max AI task-tree depth (1 = root only).</summary>
    public int MaxTreeDepth { get; set; } = AiSettingsDefaults.MaxTreeDepth;

    /// <summary>Max nodes in an AI-generated task tree (including root).</summary>
    public int MaxNodes { get; set; } = AiSettingsDefaults.MaxNodes;

    /// <summary>
    /// LLM sampling temperature for AI calls (completions + function calling).
    /// Lower = more deterministic.
    /// </summary>
    public double Temperature { get; set; } = AiSamplingDefaults.Temperature;

    /// <summary>Default LLM for new AI chats.</summary>
    public Guid? DefaultModelId { get; set; }
    public AiModel? DefaultModel { get; set; }

    /// <summary>LLM used for document chunk enrichment (translation / keywords) before embedding.</summary>
    public Guid? EnrichmentModelId { get; set; }
    public AiModel? EnrichmentModel { get; set; }

    public static AiSettings CreateDefault(Guid? modelId = null) =>
        new()
        {
            MaxTreeDepth = AiSettingsDefaults.MaxTreeDepth,
            MaxNodes = AiSettingsDefaults.MaxNodes,
            Temperature = AiSamplingDefaults.Temperature,
            DefaultModelId = modelId,
            EnrichmentModelId = modelId,
        };

    public AiSettings Clone() =>
        new()
        {
            MaxTreeDepth = MaxTreeDepth,
            MaxNodes = MaxNodes,
            Temperature = Temperature,
            DefaultModelId = DefaultModelId,
            EnrichmentModelId = EnrichmentModelId,
        };
}
