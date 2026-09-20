namespace Skemex.Application.Services.Ai;

/// <summary>In-code defaults for the task_decomposition agent tool (also used for EF seed).</summary>
public static class TaskDecompositionToolDefaults
{
    public const string SystemName = "task_decomposition";

    public const string Description =
        "Analyzes technical specifications, requirements, or documents and decomposes them into structured backlog items (Feature / Task / Bug) with acceptance criteria, risks, test cases, estimates, and assignee suggestions.";

    // Use explicit \n so EF HasData / snapshots stay OS-stable (raw """ strings inherit CRLF on Windows).
    public const string SystemPrompt =
        "You break product goals into work items for a Jira-like tracker.\n" +
        "Reply with ONLY one JSON object. No markdown fences. No commentary outside JSON.\n" +
        "\n" +
        "Every node MUST have \"type\": \"Feature\" | \"Task\" | \"Bug\" (case-sensitive).\n" +
        "1) Nodes with subtasks MUST be \"Feature\". 2) Leaves (subtasks:[]) MUST be \"Task\" or \"Bug\".\n" +
        "3) Children are never \"Feature\". 4) Never omit type or invent other types.\n" +
        "\n" +
        "DESCRIPTION QUALITY (critical): each description must be a useful implementation brief — not a one-liner like \"Do X\".\n" +
        "Cover all of: Goal (what/why), Approach (how — steps, components, constraints), Expected result (how to know it is done).\n" +
        "Prefer 80–400 words for leaves; allow simple HTML (<p>, <br>, <strong>, <em>, <ul>, <ol>, <li>, <code>, <h3>).\n" +
        "Stay under __MAX_DESCRIPTION__ characters.\n" +
        "\n" +
        "Shape: {\"root\":{type,title,description,acceptanceCriteria[],risks[],testCases[{caseType,description,expectedResult}],estimatedHours,remainingHours,storyPoints,assigneeId,subtasks[]}}\n" +
        "Limits: max depth __MAX_DEPTH__; prefer 3-8 children (max __MAX_CHILDREN__ under root, __MAX_NODES__ total);\n" +
        "title ≤ __MAX_TITLE__; 2-6 acceptanceCriteria (≤ __MAX_AC_ITEM__); 3-6 risks (max __MAX_RISKS__, ≤ __MAX_RISK_ITEM__);\n" +
        "2-6 testCases with positive and negative (≤ __MAX_TEST_FIELD__); remainingHours=estimatedHours;\n" +
        "Feature estimate = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__.\n" +
        "RESOURCE ASSIGNMENT (critical):\n" +
        "Evaluate each leaf against [ACTIVE_SPECIALIZATIONS] title|description to pick the matching domain.\n" +
        "assigneeId on Task/Bug MUST be a UUID from [PROJECT_MEMBERS] whose specializations and skills match that domain; never invent ids.\n" +
        "Features always null assigneeId. These datasets arrive under ### Context for tool: task_decomposition (chat) or the direct [ACTIVE_SPECIALIZATIONS]/[PROJECT_MEMBERS] block.\n" +
        "\n" +
        "Final gate: parents with children = Feature; leaves = Task/Bug; every description includes goal + approach + expected result.";

    public static readonly Guid SeedId = Guid.Parse("b24c28f3-8b7a-4ec9-8d4e-2895694a1122");
    public static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
