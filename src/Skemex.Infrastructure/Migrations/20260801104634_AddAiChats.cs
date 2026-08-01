using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiChatId",
                table: "ai_decomposition_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserMessageId",
                table: "ai_decomposition_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_chats_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_chats_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    DecompositionJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_chat_messages_ai_chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_chat_messages_ai_decomposition_jobs_DecompositionJobId",
                        column: x => x.DecompositionJobId,
                        principalTable: "ai_decomposition_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ai_chat_messages_project_tasks_RootTaskId",
                        column: x => x.RootTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_AiChatId",
                table: "ai_decomposition_jobs",
                column: "AiChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_decomposition_jobs_UserMessageId",
                table: "ai_decomposition_jobs",
                column: "UserMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_ChatId_CreatedAt",
                table: "ai_chat_messages",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_DecompositionJobId",
                table: "ai_chat_messages",
                column: "DecompositionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_RootTaskId",
                table: "ai_chat_messages",
                column: "RootTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_messages_TenantId_ChatId",
                table: "ai_chat_messages",
                columns: new[] { "TenantId", "ChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chats_CreatedByUserId",
                table: "ai_chats",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_chats_ProjectId_CreatedByUserId_UpdatedAt",
                table: "ai_chats",
                columns: new[] { "ProjectId", "CreatedByUserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chats_TenantId_ProjectId",
                table: "ai_chats",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ai_decomposition_jobs_ai_chat_messages_UserMessageId",
                table: "ai_decomposition_jobs",
                column: "UserMessageId",
                principalTable: "ai_chat_messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_decomposition_jobs_ai_chats_AiChatId",
                table: "ai_decomposition_jobs",
                column: "AiChatId",
                principalTable: "ai_chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_decomposition_jobs_ai_chat_messages_UserMessageId",
                table: "ai_decomposition_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_decomposition_jobs_ai_chats_AiChatId",
                table: "ai_decomposition_jobs");

            migrationBuilder.DropTable(
                name: "ai_chat_messages");

            migrationBuilder.DropTable(
                name: "ai_chats");

            migrationBuilder.DropIndex(
                name: "IX_ai_decomposition_jobs_AiChatId",
                table: "ai_decomposition_jobs");

            migrationBuilder.DropIndex(
                name: "IX_ai_decomposition_jobs_UserMessageId",
                table: "ai_decomposition_jobs");

            migrationBuilder.DropColumn(
                name: "AiChatId",
                table: "ai_decomposition_jobs");

            migrationBuilder.DropColumn(
                name: "UserMessageId",
                table: "ai_decomposition_jobs");
        }
    }
}
