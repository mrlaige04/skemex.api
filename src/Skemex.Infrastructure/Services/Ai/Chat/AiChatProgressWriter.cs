using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai.Chat;

public sealed class AiChatProgressWriter(
    ITenantRepository<AiChatMessage> messageRepository,
    ILogger<AiChatProgressWriter> logger) : IAiChatProgressWriter
{
    public async Task WriteAsync(
        Guid tenantId,
        Guid? chatId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (chatId is not Guid id || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var content = message.Trim();
        if (content.Length > 500)
        {
            content = content[..500].TrimEnd() + "…";
        }

        try
        {
            await messageRepository.AddAsync(
                new AiChatMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ChatId = id,
                    Role = AiChatMessageRole.System,
                    Content = content,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to append AI progress message to chat {ChatId}.",
                id);
        }
    }
}
