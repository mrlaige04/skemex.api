using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Queries.Projects.ListAiChats;

public sealed class ListAiChatsQuery : IQuery<IReadOnlyList<AiChatSummaryDto>>
{
    public Guid ProjectId { get; init; }
}
