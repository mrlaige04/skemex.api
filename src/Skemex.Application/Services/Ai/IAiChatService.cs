using Skemex.Application.Models.Ai;

namespace Skemex.Application.Services.Ai;

/// <summary>
/// Application façade for AI completion and model catalog listing.
/// </summary>
public interface IAiChatService
{
    Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs models from the configured provider into the local catalog (when needed) and returns active models.
    /// </summary>
    Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
