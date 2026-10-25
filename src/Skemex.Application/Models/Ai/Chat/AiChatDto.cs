using Skemex.Application.Models.Ai.Providers;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Models.Ai.Chat;

public sealed class AiChatDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? AiModelId { get; set; }
    public AiModelDto? AiModel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<AiChatMessageDto> Messages { get; set; } = [];

    public static AiChatDto FromEntity(AiChat chat) =>
        new()
        {
            Id = chat.Id,
            ProjectId = chat.ProjectId,
            Title = chat.Title,
            AiModelId = chat.AiModelId,
            AiModel = chat.AiModel is null ? null : AiModelDto.FromEntity(chat.AiModel),
            CreatedAt = chat.CreatedAt,
            UpdatedAt = chat.UpdatedAt,
            Messages = chat.Messages
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(AiChatMessageDto.FromEntity)
                .ToList(),
        };
}
