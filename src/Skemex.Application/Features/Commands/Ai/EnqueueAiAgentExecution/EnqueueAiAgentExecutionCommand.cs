using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;
using Skemex.Domain.Enums;

namespace Skemex.Application.Features.Commands.Ai.EnqueueAiAgentExecution;

public sealed class EnqueueAiAgentExecutionCommand : ICommand<AiAgentJobDto>
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public string? ToolName { get; init; }
    public required string UserInput { get; init; }
    public string? ArgumentsJson { get; init; }
    public string? Model { get; init; }

    /// <summary>Optional file uploads to attach to this execution.</summary>
    public IReadOnlyList<AiChatAttachmentUpload>? Attachments { get; init; }

    /// <summary>Optional linked project artefacts (tasks / users / documents).</summary>
    public IReadOnlyList<AiChatArtefactRef>? Artefacts { get; init; }

    /// <summary>Optional explicit source; inferred from chat/tool when null.</summary>
    public AiExecutionSource? Source { get; init; }
}
