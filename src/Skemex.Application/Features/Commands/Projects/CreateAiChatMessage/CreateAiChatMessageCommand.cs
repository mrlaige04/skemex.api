using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChatMessage;

public sealed class CreateAiChatMessageCommand : ICommand<AiChatMessageDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
    public AiChatMessageRole Role { get; init; } = AiChatMessageRole.User;
    public required string Content { get; init; }
}
