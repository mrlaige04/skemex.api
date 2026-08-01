using Skemex.Domain.Entities.Ai;

namespace Skemex.Web.Models.Projects;

public sealed class CreateAiChatRequest
{
    public string? Title { get; set; }
}

public sealed class UpdateAiChatRequest
{
    public required string Title { get; set; }
}

public sealed class CreateAiChatMessageRequest
{
    public string Role { get; set; } = nameof(AiChatMessageRole.User);
    public required string Content { get; set; }
}

public sealed class UpdateAiChatMessageRequest
{
    public required string Content { get; set; }
}
