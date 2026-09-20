namespace Skemex.Application.Services.Ai;

/// <summary>
/// Polly-backed retries for AI generation attempts that fail parse / semantic validation.
/// </summary>
public interface IAiSemanticRetry
{
    /// <summary>
    /// Executes <paramref name="attempt"/> and retries when it throws
    /// <see cref="AiSemanticValidationException"/>.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> attempt,
        CancellationToken cancellationToken = default);
}
