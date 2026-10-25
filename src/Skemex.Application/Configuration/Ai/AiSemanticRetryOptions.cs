namespace Skemex.Application.Configuration.Ai;

public sealed class AiSemanticRetryOptions
{
    /// <summary>Retries after the initial attempt when model output fails parse/validation.</summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>Delay between semantic re-queries.</summary>
    public double DelayMilliseconds { get; set; } = 750;
}
