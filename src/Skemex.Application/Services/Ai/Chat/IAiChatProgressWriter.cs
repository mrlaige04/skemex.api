namespace Skemex.Application.Services.Ai.Chat;

/// <summary>
/// Appends short, user-visible progress notes to an AI chat while a job runs.
/// </summary>
public interface IAiChatProgressWriter
{
    Task WriteAsync(
        Guid tenantId,
        Guid? chatId,
        string message,
        CancellationToken cancellationToken = default);
}
