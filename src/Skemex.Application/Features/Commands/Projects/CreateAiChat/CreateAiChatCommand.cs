using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChat;

public sealed class CreateAiChatCommand : ICommand<AiChatDto>
{
    public Guid ProjectId { get; init; }
    public string? Title { get; init; }
}
