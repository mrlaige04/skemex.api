using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureIntegrationsAndWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_integration_connections_IntegrationCon~",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_connections_projects_ProjectId",
                table: "integration_connections");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TenantId_IntegrationConnectionId_Local~",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_connections_WebhookSecret",
                table: "integration_connections");

            // Rename connection → integration (preserve rows).
            migrationBuilder.RenameTable(
                name: "integration_connections",
                newName: "integrations");

            migrationBuilder.RenameIndex(
                name: "IX_integration_connections_ProjectId",
                table: "integrations",
                newName: "IX_integrations_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_connections_TenantId_ProjectId_Provider",
                table: "integrations",
                newName: "IX_integrations_TenantId_ProjectId_Provider");

            migrationBuilder.AddForeignKey(
                name: "FK_integrations_projects_ProjectId",
                table: "integrations",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.RenameColumn(
                name: "IntegrationConnectionId",
                table: "integration_mappings",
                newName: "IntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_TenantId_IntegrationConnectionId_Exter~",
                table: "integration_mappings",
                newName: "IX_integration_mappings_TenantId_IntegrationId_ExternalEntityT~");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_IntegrationConnectionId",
                table: "integration_mappings",
                newName: "IX_integration_mappings_IntegrationId");

            migrationBuilder.CreateTable(
                name: "integration_webhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalWebhookId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Secret = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubscribedEventTypes = table.Column<long>(type: "bigint", nullable: false),
                    EnabledEventTypes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_webhooks_integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // OAuthDynamic flags = 63 (bits 0-5), DefaultEnabled All = 16383 (bits 0-13).
            // Migrate existing webhook columns into IntegrationWebhook rows.
            migrationBuilder.Sql("""
                INSERT INTO integration_webhooks (
                    "Id", "IntegrationId", "ExternalWebhookId", "Secret", "RegisteredAt",
                    "SubscribedEventTypes", "EnabledEventTypes", "CreatedAt", "UpdatedAt", "TenantId")
                SELECT
                    gen_random_uuid(),
                    i."Id",
                    i."WebhookId",
                    COALESCE(NULLIF(i."WebhookSecret", ''), replace(gen_random_uuid()::text || gen_random_uuid()::text, '-', '')),
                    i."WebhookRegisteredAt",
                    63,
                    16383,
                    COALESCE(i."CreatedAt", NOW()),
                    i."UpdatedAt",
                    i."TenantId"
                FROM integrations i
                WHERE i."WebhookSecret" IS NOT NULL OR i."WebhookId" IS NOT NULL OR i."WebhookEventSettingsJson" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "WebhookEventSettingsJson",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "WebhookId",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "WebhookRegisteredAt",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "WebhookSecret",
                table: "integrations");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TenantId_ProjectId_Provider_LocalEntit~",
                table: "integration_mappings",
                columns: new[] { "TenantId", "ProjectId", "Provider", "LocalEntityType", "LocalEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_webhooks_IntegrationId",
                table: "integration_webhooks",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_webhooks_Secret",
                table: "integration_webhooks",
                column: "Secret",
                unique: true,
                filter: "\"Secret\" IS NOT NULL AND \"Secret\" <> ''");

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_integrations_IntegrationId",
                table: "integration_mappings",
                column: "IntegrationId",
                principalTable: "integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_integrations_IntegrationId",
                table: "integration_mappings");

            migrationBuilder.DropTable(
                name: "integration_webhooks");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TenantId_ProjectId_Provider_LocalEntit~",
                table: "integration_mappings");

            migrationBuilder.AddColumn<string>(
                name: "WebhookEventSettingsJson",
                table: "integrations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookId",
                table: "integrations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WebhookRegisteredAt",
                table: "integrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookSecret",
                table: "integrations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "IntegrationId",
                table: "integration_mappings",
                newName: "IntegrationConnectionId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_TenantId_IntegrationId_ExternalEntityT~",
                table: "integration_mappings",
                newName: "IX_integration_mappings_TenantId_IntegrationConnectionId_Exter~");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_IntegrationId",
                table: "integration_mappings",
                newName: "IX_integration_mappings_IntegrationConnectionId");

            migrationBuilder.DropForeignKey(
                name: "FK_integrations_projects_ProjectId",
                table: "integrations");

            migrationBuilder.RenameTable(
                name: "integrations",
                newName: "integration_connections");

            migrationBuilder.RenameIndex(
                name: "IX_integrations_ProjectId",
                table: "integration_connections",
                newName: "IX_integration_connections_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_integrations_TenantId_ProjectId_Provider",
                table: "integration_connections",
                newName: "IX_integration_connections_TenantId_ProjectId_Provider");

            migrationBuilder.AddForeignKey(
                name: "FK_integration_connections_projects_ProjectId",
                table: "integration_connections",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TenantId_IntegrationConnectionId_Local~",
                table: "integration_mappings",
                columns: new[] { "TenantId", "IntegrationConnectionId", "LocalEntityType", "LocalEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_WebhookSecret",
                table: "integration_connections",
                column: "WebhookSecret",
                unique: true,
                filter: "\"WebhookSecret\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_integration_connections_IntegrationCon~",
                table: "integration_mappings",
                column: "IntegrationConnectionId",
                principalTable: "integration_connections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
