using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChatMessage;

public sealed class UpdateAiChatMessageCommand : ICommand<AiChatMessageDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
    public Guid MessageId { get; init; }
    public required string Content { get; init; }
}
