using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedProjectQaAgentTool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "agent_tools",
                columns: new[] { "Id", "CreatedAt", "Description", "SystemName", "SystemPrompt", "UpdatedAt" },
                values: new object[] { new Guid("d4e5f6a7-8b9c-4d0e-1f2a-3b4c5d6e7f8a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Use this tool when the user asks a question about the project: architecture, domain concepts, uploaded documentation, backlog status, existing tasks/issues, assignees, or how something works. Prefer this tool for Q&A, explanations, lookups, and status questions. Do not use it to create or decompose a new backlog tree — use task_decomposition for that.", "project_qa", "You are a project knowledge assistant for an engineering delivery product.\nAnswer the user's question using ONLY the information under ### CONTEXT.\nIf context is missing, incomplete, or contradictory, say what you cannot verify — never invent facts.\nCite concrete sources when available: task keys (e.g. ABC-12) and document titles/file names.\nRespond in clear Markdown (headings, bullet lists, short paragraphs as needed).\nAlways write in the same primary language as the user's request.\nDo not output JSON. Do not wrap the entire answer in a markdown code fence.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "agent_tools",
                keyColumn: "Id",
                keyValue: new Guid("d4e5f6a7-8b9c-4d0e-1f2a-3b4c5d6e7f8a"));
        }
    }
}
