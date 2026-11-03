using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraWebhookEventSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookEventSettingsJson",
                table: "integration_connections",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_WebhookSecret",
                table: "integration_connections",
                column: "WebhookSecret",
                unique: true,
                filter: "\"WebhookSecret\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_integration_connections_WebhookSecret",
                table: "integration_connections");

            migrationBuilder.DropColumn(
                name: "WebhookEventSettingsJson",
                table: "integration_connections");
        }
    }
}
