using Skemex.Application.Models.Ai;

namespace Skemex.Application.Services.Ai;

/// <summary>
/// Thin façade over Microsoft.Extensions.AI <c>IChatClient</c>.
/// </summary>
public interface IAiChatService
{
    Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
