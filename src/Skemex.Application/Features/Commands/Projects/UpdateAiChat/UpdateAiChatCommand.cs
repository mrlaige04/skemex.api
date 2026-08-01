using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChat;

public sealed class UpdateAiChatCommand : ICommand<AiChatSummaryDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
    public required string Title { get; init; }
}
