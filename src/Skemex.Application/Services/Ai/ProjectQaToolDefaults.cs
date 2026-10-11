namespace Skemex.Application.Services.Ai;

/// <summary>In-code defaults for the project_qa agent tool (also used for EF seed).</summary>
public static class ProjectQaToolDefaults
{
    public const string SystemName = "project_qa";

    public const string Description =
        "Use this tool when the user asks a question about the project: architecture, domain concepts, " +
        "uploaded documentation, backlog status, existing tasks/issues, assignees, or how something works. " +
        "Prefer this tool for Q&A, explanations, lookups, and status questions. " +
        "Do not use it to create or decompose a new backlog tree — use task_decomposition for that.";

    // Explicit \n keeps EF HasData / snapshots OS-stable (raw """ strings inherit CRLF on Windows).
    public const string SystemPrompt =
        "You are a project knowledge assistant for an engineering delivery product.\n" +
        "Answer the user's question using ONLY the information under ### CONTEXT.\n" +
        "If context is missing, incomplete, or contradictory, say what you cannot verify — never invent facts.\n" +
        "Cite concrete sources when available: task keys (e.g. ABC-12) and document titles/file names.\n" +
        "Respond in clear Markdown (headings, bullet lists, short paragraphs as needed).\n" +
        "Always write in the same primary language as the user's request.\n" +
        "Do not output JSON. Do not wrap the entire answer in a markdown code fence.";

    public static readonly Guid SeedId = Guid.Parse("d4e5f6a7-8b9c-4d0e-1f2a-3b4c5d6e7f8a");
    public static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
