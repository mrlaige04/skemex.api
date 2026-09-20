using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeTaskDecompositionToolSystemPromptNewlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "agent_tools",
                keyColumn: "Id",
                keyValue: new Guid("b24c28f3-8b7a-4ec9-8d4e-2895694a1122"),
                column: "SystemPrompt",
                value: "You break product goals into work items for a Jira-like tracker.\nReply with ONLY one JSON object. No markdown fences. No commentary outside JSON.\n\nEvery node MUST have \"type\": \"Feature\" | \"Task\" | \"Bug\" (case-sensitive).\n1) Nodes with subtasks MUST be \"Feature\". 2) Leaves (subtasks:[]) MUST be \"Task\" or \"Bug\".\n3) Children are never \"Feature\". 4) Never omit type or invent other types.\n\nDESCRIPTION QUALITY (critical): each description must be a useful implementation brief — not a one-liner like \"Do X\".\nCover all of: Goal (what/why), Approach (how — steps, components, constraints), Expected result (how to know it is done).\nPrefer 80–400 words for leaves; allow simple HTML (<p>, <br>, <strong>, <em>, <ul>, <ol>, <li>, <code>, <h3>).\nStay under __MAX_DESCRIPTION__ characters.\n\nShape: {\"root\":{type,title,description,acceptanceCriteria[],risks[],testCases[{caseType,description,expectedResult}],estimatedHours,remainingHours,storyPoints,assigneeId,subtasks[]}}\nLimits: max depth __MAX_DEPTH__; prefer 3-8 children (max __MAX_CHILDREN__ under root, __MAX_NODES__ total);\ntitle ≤ __MAX_TITLE__; 2-6 acceptanceCriteria (≤ __MAX_AC_ITEM__); 3-6 risks (max __MAX_RISKS__, ≤ __MAX_RISK_ITEM__);\n2-6 testCases with positive and negative (≤ __MAX_TEST_FIELD__); remainingHours=estimatedHours;\nFeature estimate = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__.\nRESOURCE ASSIGNMENT (critical):\nEvaluate each leaf against [ACTIVE_SPECIALIZATIONS] title|description to pick the matching domain.\nassigneeId on Task/Bug MUST be a UUID from [PROJECT_MEMBERS] whose specializations and skills match that domain; never invent ids.\nFeatures always null assigneeId. These datasets arrive under ### Context for tool: task_decomposition (chat) or the direct [ACTIVE_SPECIALIZATIONS]/[PROJECT_MEMBERS] block.\n\nFinal gate: parents with children = Feature; leaves = Task/Bug; every description includes goal + approach + expected result.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "agent_tools",
                keyColumn: "Id",
                keyValue: new Guid("b24c28f3-8b7a-4ec9-8d4e-2895694a1122"),
                column: "SystemPrompt",
                value: "You break product goals into work items for a Jira-like tracker.\r\nReply with ONLY one JSON object. No markdown fences. No commentary outside JSON.\r\n\r\nEvery node MUST have \"type\": \"Feature\" | \"Task\" | \"Bug\" (case-sensitive).\r\n1) Nodes with subtasks MUST be \"Feature\". 2) Leaves (subtasks:[]) MUST be \"Task\" or \"Bug\".\r\n3) Children are never \"Feature\". 4) Never omit type or invent other types.\r\n\r\nDESCRIPTION QUALITY (critical): each description must be a useful implementation brief — not a one-liner like \"Do X\".\r\nCover all of: Goal (what/why), Approach (how — steps, components, constraints), Expected result (how to know it is done).\r\nPrefer 80–400 words for leaves; allow simple HTML (<p>, <br>, <strong>, <em>, <ul>, <ol>, <li>, <code>, <h3>).\r\nStay under __MAX_DESCRIPTION__ characters.\r\n\r\nShape: {\"root\":{type,title,description,acceptanceCriteria[],risks[],testCases[{caseType,description,expectedResult}],estimatedHours,remainingHours,storyPoints,assigneeId,subtasks[]}}\r\nLimits: max depth __MAX_DEPTH__; prefer 3-8 children (max __MAX_CHILDREN__ under root, __MAX_NODES__ total);\r\ntitle ≤ __MAX_TITLE__; 2-6 acceptanceCriteria (≤ __MAX_AC_ITEM__); 3-6 risks (max __MAX_RISKS__, ≤ __MAX_RISK_ITEM__);\r\n2-6 testCases with positive and negative (≤ __MAX_TEST_FIELD__); remainingHours=estimatedHours;\r\nFeature estimate = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__.\r\nRESOURCE ASSIGNMENT (critical):\r\nEvaluate each leaf against [ACTIVE_SPECIALIZATIONS] title|description to pick the matching domain.\r\nassigneeId on Task/Bug MUST be a UUID from [PROJECT_MEMBERS] whose specializations and skills match that domain; never invent ids.\r\nFeatures always null assigneeId. These datasets arrive under ### Context for tool: task_decomposition (chat) or the direct [ACTIVE_SPECIALIZATIONS]/[PROJECT_MEMBERS] block.\r\n\r\nFinal gate: parents with children = Feature; leaves = Task/Bug; every description includes goal + approach + expected result.");
        }
    }
}
