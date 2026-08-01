namespace Skemex.Web.Models.Projects;

public sealed class CreateAiChatRequest
{
    public string? Title { get; set; }
    public Guid? AiModelId { get; set; }
}

public sealed class UpdateAiChatRequest
{
    public string? Title { get; set; }
    public Guid? AiModelId { get; set; }
    public bool ClearAiModel { get; set; }
}

public sealed class CreateAiChatMessageRequest
{
    public string Role { get; set; } = "User";
    public required string Content { get; set; }
}

public sealed class UpdateAiChatMessageRequest
{
    public required string Content { get; set; }
}
