namespace Skemex.Application.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Optional fallback provider key when a model is not found in the local catalog,
    /// or when resolving the "active" provider. If empty/missing, the first enabled DB provider is used.
    /// </summary>
    public string ActiveProvider { get; set; } = string.Empty;
}
