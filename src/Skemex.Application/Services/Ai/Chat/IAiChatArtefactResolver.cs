using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;

namespace Skemex.Application.Services.Ai.Chat;

public sealed class AiChatArtefactResolution
{
    /// <summary>Markdown block with [ARTEFACTS: #|Type|Content] for Task/User rows.</summary>
    public string? PromptTableMarkdown { get; init; }

    /// <summary>Document artefacts loaded as multimodal completion attachments.</summary>
    public IReadOnlyList<AiCompletionAttachment> DocumentAttachments { get; init; } = [];
}

public interface IAiChatArtefactResolver
{
    Task<AiChatArtefactResolution> ResolveAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyList<AiChatArtefactRef> artefacts,
        CancellationToken cancellationToken = default);
}
