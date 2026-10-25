using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Completions;

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
