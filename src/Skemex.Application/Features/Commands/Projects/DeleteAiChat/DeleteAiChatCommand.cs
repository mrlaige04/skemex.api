using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteAiChat;

public sealed class DeleteAiChatCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
}
