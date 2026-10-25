namespace Skemex.Application.Configuration.Ai;

/// <summary>Resilience settings for AI HTTP transport and semantic (parse/validate) retries.</summary>
public sealed class AiResilienceOptions
{
    public const string SectionName = "AiResilience";

    public AiHttpRetryOptions Http { get; set; } = new();

    public AiSemanticRetryOptions Semantic { get; set; } = new();
}
