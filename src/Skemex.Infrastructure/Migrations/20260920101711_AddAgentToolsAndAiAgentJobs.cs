using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentToolsAndAiAgentJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_tools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserInput = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CustomInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssistantMessage = table.Column<string>(type: "text", nullable: true),
                    ArtifactType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArtifactPayloadJson = table.Column<string>(type: "text", nullable: true),
                    HangfireJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_agent_jobs_ai_chats_AiChatId",
                        column: x => x.AiChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_agent_jobs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_agent_jobs_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "agent_tools",
                columns: new[] { "Id", "CreatedAt", "Description", "SystemName", "SystemPrompt", "UpdatedAt" },
                values: new object[] { new Guid("b24c28f3-8b7a-4ec9-8d4e-2895694a1122"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Analyzes technical specifications, requirements, or documents and decomposes them into structured backlog items (Feature / Task / Bug) with acceptance criteria, risks, test cases, estimates, and assignee suggestions.", "task_decomposition", "You break product goals into work items for a Jira-like tracker.\r\nReply with ONLY one JSON object. No markdown fences. No commentary outside JSON.\r\n\r\nEvery node MUST have \"type\": \"Feature\" | \"Task\" | \"Bug\" (case-sensitive).\r\n1) Nodes with subtasks MUST be \"Feature\". 2) Leaves (subtasks:[]) MUST be \"Task\" or \"Bug\".\r\n3) Children are never \"Feature\". 4) Never omit type or invent other types.\r\n\r\nDESCRIPTION QUALITY (critical): each description must be a useful implementation brief — not a one-liner like \"Do X\".\r\nCover all of: Goal (what/why), Approach (how — steps, components, constraints), Expected result (how to know it is done).\r\nPrefer 80–400 words for leaves; allow simple HTML (<p>, <br>, <strong>, <em>, <ul>, <ol>, <li>, <code>, <h3>).\r\nStay under __MAX_DESCRIPTION__ characters.\r\n\r\nShape: {\"root\":{type,title,description,acceptanceCriteria[],risks[],testCases[{caseType,description,expectedResult}],estimatedHours,remainingHours,storyPoints,assigneeId,subtasks[]}}\r\nLimits: max depth __MAX_DEPTH__; prefer 3-8 children (max __MAX_CHILDREN__ under root, __MAX_NODES__ total);\r\ntitle ≤ __MAX_TITLE__; 2-6 acceptanceCriteria (≤ __MAX_AC_ITEM__); 3-6 risks (max __MAX_RISKS__, ≤ __MAX_RISK_ITEM__);\r\n2-6 testCases with positive and negative (≤ __MAX_TEST_FIELD__); remainingHours=estimatedHours;\r\nFeature estimate = sum of children; estimatedHours ≤ __MAX_ESTIMATE_HOURS__.\r\nAssign leaf assigneeId from available_members when possible; Features always null assigneeId.\r\n\r\nFinal gate: parents with children = Feature; leaves = Task/Bug; every description includes goal + approach + expected result.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_agent_tools_SystemName",
                table: "agent_tools",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_jobs_AiChatId",
                table: "ai_agent_jobs",
                column: "AiChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_jobs_ProjectId_CreatedAt",
                table: "ai_agent_jobs",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_jobs_RequestedByUserId",
                table: "ai_agent_jobs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_jobs_TenantId_CreatedAt",
                table: "ai_agent_jobs",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_tools");

            migrationBuilder.DropTable(
                name: "ai_agent_jobs");
        }
    }
}
