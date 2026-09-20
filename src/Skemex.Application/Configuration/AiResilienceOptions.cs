namespace Skemex.Application.Configuration;

/// <summary>Resilience settings for AI HTTP transport and semantic (parse/validate) retries.</summary>
public sealed class AiResilienceOptions
{
    public const string SectionName = "AiResilience";

    public AiHttpRetryOptions Http { get; set; } = new();

    public AiSemanticRetryOptions Semantic { get; set; } = new();
}

public sealed class AiHttpRetryOptions
{
    /// <summary>Retries after the initial attempt (total attempts = MaxRetryAttempts + 1).</summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Base delay before the first retry (exponential backoff + jitter).</summary>
    public double InitialDelaySeconds { get; set; } = 1.5;
}

public sealed class AiSemanticRetryOptions
{
    /// <summary>Retries after the initial attempt when model output fails parse/validation.</summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Delay between semantic re-queries.</summary>
    public double DelayMilliseconds { get; set; } = 750;
}
