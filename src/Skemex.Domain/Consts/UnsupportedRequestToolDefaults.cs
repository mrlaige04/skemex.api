namespace Skemex.Domain.Consts;

/// <summary>In-code defaults for the unsupported_request agent tool (also used for EF seed).</summary>
public static class UnsupportedRequestToolDefaults
{
    public const string SystemName = "unsupported_request";

    public const string Description =
        "Use this tool when the user message is not a project question and not a request to create or decompose " +
        "backlog work. Prefer it for offtopic chat, spam, gibberish, jokes, greetings with no actionable ask, " +
        "or anything outside Skemex project Q&A / task decomposition. " +
        "Do not use it for architecture/docs/backlog questions (use project_qa) or for breaking down features " +
        "into tasks (use task_decomposition).";

    /// <summary>No Stage-2 LLM prompt — the tool returns a fixed user-facing reply.</summary>
    public const string SystemPrompt = "";

    public const string UserFacingReply =
        "I couldn't match that to something I can help with here. " +
        "Try asking a question about this project (docs, architecture, or backlog), " +
        "or describe a feature/goal you'd like broken into tasks.";

    public static readonly Guid SeedId = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");
    public static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
