using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Models.Ai.Chat;

public sealed class AiChatMessageDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? RootTaskId { get; set; }
    public string? RootTaskCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static AiChatMessageDto FromEntity(AiChatMessage message) =>
        new()
        {
            Id = message.Id,
            ChatId = message.ChatId,
            Role = message.Role.ToString(),
            Content = message.Content,
            RootTaskId = message.RootTaskId,
            RootTaskCode = message.RootTask?.Code,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
        };
}
