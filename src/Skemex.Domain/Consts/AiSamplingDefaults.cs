namespace Skemex.Domain.Consts;

/// <summary>Default / bounds for tenant-configurable LLM sampling.</summary>
public static class AiSamplingDefaults
{
    /// <summary>Stable default for structured agent tools (JSON trees, routing).</summary>
    public const double Temperature = 0.2;

    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 2.0;
}
