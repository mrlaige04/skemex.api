using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Queries.Projects.GetAiChat;

public sealed class GetAiChatQuery : IQuery<AiChatDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
}
