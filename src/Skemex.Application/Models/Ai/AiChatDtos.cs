using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Models.Ai;

public sealed class AiChatSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? AiModelId { get; set; }
    public AiModelDto? AiModel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static AiChatSummaryDto FromEntity(AiChat chat) =>
        new()
        {
            Id = chat.Id,
            ProjectId = chat.ProjectId,
            Title = chat.Title,
            AiModelId = chat.AiModelId,
            AiModel = chat.AiModel is null ? null : AiModelDto.FromEntity(chat.AiModel),
            CreatedAt = chat.CreatedAt,
            UpdatedAt = chat.UpdatedAt,
        };
}

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

public sealed class AiChatMessageDto
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid? DecompositionJobId { get; set; }
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
            DecompositionJobId = message.DecompositionJobId,
            RootTaskId = message.RootTaskId,
            RootTaskCode = message.RootTask?.Code,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
        };
}
