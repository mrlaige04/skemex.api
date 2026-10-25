using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Features.Queries.Projects.GetAiChat;

public sealed class GetAiChatQuery : IQuery<AiChatDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
}
