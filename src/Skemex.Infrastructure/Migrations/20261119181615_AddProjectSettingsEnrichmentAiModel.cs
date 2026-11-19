using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSettingsEnrichmentAiModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EnrichmentAiModelId",
                table: "project_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_settings_EnrichmentAiModelId",
                table: "project_settings",
                column: "EnrichmentAiModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_project_settings_ai_models_EnrichmentAiModelId",
                table: "project_settings",
                column: "EnrichmentAiModelId",
                principalTable: "ai_models",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_project_settings_ai_models_EnrichmentAiModelId",
                table: "project_settings");

            migrationBuilder.DropIndex(
                name: "IX_project_settings_EnrichmentAiModelId",
                table: "project_settings");

            migrationBuilder.DropColumn(
                name: "EnrichmentAiModelId",
                table: "project_settings");
        }
    }
}
