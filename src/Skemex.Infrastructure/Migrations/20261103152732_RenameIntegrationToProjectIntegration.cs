using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameIntegrationToProjectIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_integrations_IntegrationId",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_webhooks_integrations_IntegrationId",
                table: "integration_webhooks");

            // May still be named from older renames (integration_connections → integrations).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1 FROM pg_constraint c
                    JOIN pg_class t ON c.conrelid = t.oid
                    WHERE t.relname = 'integrations' AND c.conname = 'FK_integrations_projects_ProjectId')
                  THEN
                    ALTER TABLE integrations DROP CONSTRAINT "FK_integrations_projects_ProjectId";
                  ELSIF EXISTS (
                    SELECT 1 FROM pg_constraint c
                    JOIN pg_class t ON c.conrelid = t.oid
                    WHERE t.relname = 'integrations' AND c.conname = 'FK_integration_connections_projects_ProjectId')
                  THEN
                    ALTER TABLE integrations DROP CONSTRAINT "FK_integration_connections_projects_ProjectId";
                  END IF;
                END $$;
                """);

            migrationBuilder.RenameTable(
                name: "integrations",
                newName: "project_integrations");

            migrationBuilder.RenameIndex(
                name: "IX_integrations_ProjectId",
                table: "project_integrations",
                newName: "IX_project_integrations_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_integrations_TenantId_ProjectId_Provider",
                table: "project_integrations",
                newName: "IX_project_integrations_TenantId_ProjectId_Provider");

            migrationBuilder.RenameColumn(
                name: "IntegrationId",
                table: "integration_webhooks",
                newName: "ProjectIntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_webhooks_IntegrationId",
                table: "integration_webhooks",
                newName: "IX_integration_webhooks_ProjectIntegrationId");

            migrationBuilder.RenameColumn(
                name: "IntegrationId",
                table: "integration_mappings",
                newName: "ProjectIntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_TenantId_IntegrationId_ExternalEntityT~",
                table: "integration_mappings",
                newName: "IX_integration_mappings_TenantId_ProjectIntegrationId_External~");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_IntegrationId",
                table: "integration_mappings",
                newName: "IX_integration_mappings_ProjectIntegrationId");

            // PostgreSQL keeps old PK name across RenameTable; align with EF snapshot.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                  pk_name text;
                BEGIN
                  SELECT c.conname INTO pk_name
                  FROM pg_constraint c
                  JOIN pg_class t ON c.conrelid = t.oid
                  WHERE t.relname = 'project_integrations' AND c.contype = 'p';

                  IF pk_name IS NOT NULL AND pk_name <> 'PK_project_integrations' THEN
                    EXECUTE format(
                      'ALTER TABLE project_integrations RENAME CONSTRAINT %I TO %I',
                      pk_name,
                      'PK_project_integrations');
                  END IF;
                END $$;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_project_integrations_projects_ProjectId",
                table: "project_integrations",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_project_integrations_ProjectIntegratio~",
                table: "integration_mappings",
                column: "ProjectIntegrationId",
                principalTable: "project_integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_webhooks_project_integrations_ProjectIntegratio~",
                table: "integration_webhooks",
                column: "ProjectIntegrationId",
                principalTable: "project_integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_project_integrations_ProjectIntegratio~",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_webhooks_project_integrations_ProjectIntegratio~",
                table: "integration_webhooks");

            migrationBuilder.DropForeignKey(
                name: "FK_project_integrations_projects_ProjectId",
                table: "project_integrations");

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                  pk_name text;
                BEGIN
                  SELECT c.conname INTO pk_name
                  FROM pg_constraint c
                  JOIN pg_class t ON c.conrelid = t.oid
                  WHERE t.relname = 'project_integrations' AND c.contype = 'p';

                  IF pk_name IS NOT NULL AND pk_name <> 'PK_integrations' THEN
                    EXECUTE format(
                      'ALTER TABLE project_integrations RENAME CONSTRAINT %I TO %I',
                      pk_name,
                      'PK_integrations');
                  END IF;
                END $$;
                """);

            migrationBuilder.RenameColumn(
                name: "ProjectIntegrationId",
                table: "integration_webhooks",
                newName: "IntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_webhooks_ProjectIntegrationId",
                table: "integration_webhooks",
                newName: "IX_integration_webhooks_IntegrationId");

            migrationBuilder.RenameColumn(
                name: "ProjectIntegrationId",
                table: "integration_mappings",
                newName: "IntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_TenantId_ProjectIntegrationId_External~",
                table: "integration_mappings",
                newName: "IX_integration_mappings_TenantId_IntegrationId_ExternalEntityT~");

            migrationBuilder.RenameIndex(
                name: "IX_integration_mappings_ProjectIntegrationId",
                table: "integration_mappings",
                newName: "IX_integration_mappings_IntegrationId");

            migrationBuilder.RenameTable(
                name: "project_integrations",
                newName: "integrations");

            migrationBuilder.RenameIndex(
                name: "IX_project_integrations_ProjectId",
                table: "integrations",
                newName: "IX_integrations_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_project_integrations_TenantId_ProjectId_Provider",
                table: "integrations",
                newName: "IX_integrations_TenantId_ProjectId_Provider");

            migrationBuilder.AddForeignKey(
                name: "FK_integrations_projects_ProjectId",
                table: "integrations",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_integrations_IntegrationId",
                table: "integration_mappings",
                column: "IntegrationId",
                principalTable: "integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_webhooks_integrations_IntegrationId",
                table: "integration_webhooks",
                column: "IntegrationId",
                principalTable: "integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
