using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiExecutionId",
                table: "ai_usage_activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiAgentJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiDecompositionJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserInputPreview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: false),
                    OperationCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_executions_ai_agent_jobs_AiAgentJobId",
                        column: x => x.AiAgentJobId,
                        principalTable: "ai_agent_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_executions_ai_chats_AiChatId",
                        column: x => x.AiChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_executions_ai_decomposition_jobs_AiDecompositionJobId",
                        column: x => x.AiDecompositionJobId,
                        principalTable: "ai_decomposition_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_executions_project_documents_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "project_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_executions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_executions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiExecutionId",
                table: "ai_usage_activities",
                column: "AiExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_AiAgentJobId",
                table: "ai_executions",
                column: "AiAgentJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_AiChatId",
                table: "ai_executions",
                column: "AiChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_AiDecompositionJobId",
                table: "ai_executions",
                column: "AiDecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_ProjectDocumentId",
                table: "ai_executions",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_ProjectId_StartedAt",
                table: "ai_executions",
                columns: new[] { "ProjectId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_TenantId_CreatedAt",
                table: "ai_executions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_TenantId_Source_CreatedAt",
                table: "ai_executions",
                columns: new[] { "TenantId", "Source", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_TenantId_StartedAt",
                table: "ai_executions",
                columns: new[] { "TenantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_UserId",
                table: "ai_executions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_usage_activities_ai_executions_AiExecutionId",
                table: "ai_usage_activities",
                column: "AiExecutionId",
                principalTable: "ai_executions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_usage_activities_ai_executions_AiExecutionId",
                table: "ai_usage_activities");

            migrationBuilder.DropTable(
                name: "ai_executions");

            migrationBuilder.DropIndex(
                name: "IX_ai_usage_activities_AiExecutionId",
                table: "ai_usage_activities");

            migrationBuilder.DropColumn(
                name: "AiExecutionId",
                table: "ai_usage_activities");
        }
    }
}
