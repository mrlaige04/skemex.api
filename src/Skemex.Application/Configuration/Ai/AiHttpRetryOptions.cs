namespace Skemex.Application.Configuration.Ai;

public sealed class AiHttpRetryOptions
{
    /// <summary>Retries after the initial attempt (total attempts = MaxRetryAttempts + 1).</summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Base delay before the first retry (exponential backoff + jitter).</summary>
    public double InitialDelaySeconds { get; set; } = 1.5;
}
