using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_chat_message_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    BlobId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_chat_message_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_chat_message_attachments_ai_chat_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ai_chat_messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_chat_message_attachments_ai_chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "ai_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_message_attachments_ChatId_CreatedAt",
                table: "ai_chat_message_attachments",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_message_attachments_MessageId_CreatedAt",
                table: "ai_chat_message_attachments",
                columns: new[] { "MessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_chat_message_attachments_TenantId_ChatId",
                table: "ai_chat_message_attachments",
                columns: new[] { "TenantId", "ChatId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_chat_message_attachments");
        }
    }
}
