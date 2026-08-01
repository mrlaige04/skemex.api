using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteAiChatMessage;

public sealed class DeleteAiChatMessageCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
    public Guid MessageId { get; init; }
}
