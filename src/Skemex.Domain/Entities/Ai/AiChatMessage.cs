using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Domain.Entities.Ai;

public enum AiChatMessageRole
{
    User = 0,
    Assistant = 1,
    System = 2,
}

public class AiChatMessage : TenantEntity
{
    public Guid ChatId { get; set; }
    public AiChat Chat { get; set; } = null!;

    public AiChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public Guid? DecompositionJobId { get; set; }
    public AiDecompositionJob? DecompositionJob { get; set; }

    public Guid? RootTaskId { get; set; }
    public ProjectTask? RootTask { get; set; }
}
