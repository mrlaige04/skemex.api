using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiDecompositionJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_chat_messages_ai_decomposition_jobs_DecompositionJobId",
                table: "ai_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_executions_ai_decomposition_jobs_AiDecompositionJobId",
                table: "ai_executions");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_usage_activities_ai_decomposition_jobs_AiDecompositionJo~",
                table: "ai_usage_activities");

            migrationBuilder.DropTable(
                name: "ai_decomposition_jobs");

            migrationBuilder.DropIndex(
                name: "IX_ai_usage_activities_AiDecompositionJobId",
                table: "ai_usage_activities");

            migrationBuilder.DropIndex(
                name: "IX_ai_executions_AiDecompositionJobId",
                table: "ai_executions");

            migrationBuilder.DropIndex(
                name: "IX_ai_chat_messages_DecompositionJobId",
                table: "ai_chat_messages");

            migrationBuilder.DropColumn(
                name: "AiDecompositionJobId",
                table: "ai_usage_activities");

            migrationBuilder.DropColumn(
                name: "AiDecompositionJobId",
                table: "ai_executions");

            migrationBuilder.DropColumn(
                name: "DecompositionJobId",
                table: "ai_chat_messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiDecompositionJobId",
                table: "ai_usage_activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiDecompositionJobId",
                table: "ai_executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecompositionJobId",
                table: "ai_chat_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_decomposition_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AiChatId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HangfireJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserInput = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_decomposition_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_decomposition_jobs_ai_chat_messages_UserMessageId",
                        column: x => x.UserMessageId,
                        principalTable: "ai_chat_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_decomposition_jobs_ai_chats_AiChatId",
                        column: x => x.AiChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_decomposition_jobs_project_tasks_RootTaskId",
                        column: x => x.RootTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_decomposition_jobs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_decomposition_jobs_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_activities_AiDecompositionJobId",
                table: "ai_usage_activities",
                column: "AiDecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_executions_AiDecompositionJobId",
                table: "ai_executions",
                column: "AiDecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_DecompositionJobId",
                table: "ai_chat_messages",
                column: "DecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_AiChatId",
                table: "ai_decomposition_jobs",
                column: "AiChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_ProjectId_CreatedAt",
                table: "ai_decomposition_jobs",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_RequestedByUserId",
                table: "ai_decomposition_jobs",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_RootTaskId",
                table: "ai_decomposition_jobs",
                column: "RootTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_TenantId_ProjectId",
                table: "ai_decomposition_jobs",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_UserMessageId",
                table: "ai_decomposition_jobs",
                column: "UserMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_chat_messages_ai_decomposition_jobs_DecompositionJobId",
                table: "ai_chat_messages",
                column: "DecompositionJobId",
                principalTable: "ai_decomposition_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_executions_ai_decomposition_jobs_AiDecompositionJobId",
                table: "ai_executions",
                column: "AiDecompositionJobId",
                principalTable: "ai_decomposition_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_usage_activities_ai_decomposition_jobs_AiDecompositionJo~",
                table: "ai_usage_activities",
                column: "AiDecompositionJobId",
                principalTable: "ai_decomposition_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
