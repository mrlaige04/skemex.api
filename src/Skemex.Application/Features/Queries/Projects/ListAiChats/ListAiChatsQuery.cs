using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Features.Queries.Projects.ListAiChats;

public sealed class ListAiChatsQuery : IQuery<IReadOnlyList<AiChatSummaryDto>>
{
    public Guid ProjectId { get; init; }
}
