using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OperationName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: true),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    InputItemCount = table.Column<int>(type: "integer", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiChatMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiAgentJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiDecompositionJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_ai_agent_jobs_AiAgentJobId",
                        column: x => x.AiAgentJobId,
                        principalTable: "ai_agent_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_ai_chat_messages_AiChatMessageId",
                        column: x => x.AiChatMessageId,
                        principalTable: "ai_chat_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_ai_chats_AiChatId",
                        column: x => x.AiChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_ai_decomposition_jobs_AiDecompositionJo~",
                        column: x => x.AiDecompositionJobId,
                        principalTable: "ai_decomposition_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_usage_activities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiAgentJobId",
                table: "ai_usage_activities",
                column: "AiAgentJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiChatId",
                table: "ai_usage_activities",
                column: "AiChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiChatMessageId",
                table: "ai_usage_activities",
                column: "AiChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiDecompositionJobId",
                table: "ai_usage_activities",
                column: "AiDecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_ParentActivityId",
                table: "ai_usage_activities",
                column: "ParentActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_ProjectDocumentId",
                table: "ai_usage_activities",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_ProjectId_CreatedAt",
                table: "ai_usage_activities",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_TenantId_CorrelationId",
                table: "ai_usage_activities",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_TenantId_CreatedAt",
                table: "ai_usage_activities",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_TenantId_OperationName_CreatedAt",
                table: "ai_usage_activities",
                columns: new[] { "TenantId", "OperationName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_TenantId_OperationType_CreatedAt",
                table: "ai_usage_activities",
                columns: new[] { "TenantId", "OperationType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_TenantId_UserId_CreatedAt",
                table: "ai_usage_activities",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_UserId",
                table: "ai_usage_activities",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_activities");
        }
    }
}
