using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260801140000_AddAiModelsAndGroq")]
public class AddAiModelsAndGroq : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ai_models",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IconKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_models", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ai_models_Provider_ExternalId",
            table: "ai_models",
            columns: new[] { "Provider", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ai_models_Provider_IsActive",
            table: "ai_models",
            columns: new[] { "Provider", "IsActive" });

        migrationBuilder.AddColumn<Guid>(
            name: "AiModelId",
            table: "ai_chats",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ai_chats_AiModelId",
            table: "ai_chats",
            column: "AiModelId");

        migrationBuilder.AddForeignKey(
            name: "FK_ai_chats_ai_models_AiModelId",
            table: "ai_chats",
            column: "AiModelId",
            principalTable: "ai_models",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddColumn<Guid>(
            name: "DefaultAiModelId",
            table: "project_settings",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_project_settings_DefaultAiModelId",
            table: "project_settings",
            column: "DefaultAiModelId");

        migrationBuilder.AddForeignKey(
            name: "FK_project_settings_ai_models_DefaultAiModelId",
            table: "project_settings",
            column: "DefaultAiModelId",
            principalTable: "ai_models",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ai_chats_ai_models_AiModelId",
            table: "ai_chats");

        migrationBuilder.DropForeignKey(
            name: "FK_project_settings_ai_models_DefaultAiModelId",
            table: "project_settings");

        migrationBuilder.DropIndex(
            name: "IX_ai_chats_AiModelId",
            table: "ai_chats");

        migrationBuilder.DropIndex(
            name: "IX_project_settings_DefaultAiModelId",
            table: "project_settings");

        migrationBuilder.DropColumn(
            name: "AiModelId",
            table: "ai_chats");

        migrationBuilder.DropColumn(
            name: "DefaultAiModelId",
            table: "project_settings");

        migrationBuilder.DropTable(
            name: "ai_models");
    }
}
