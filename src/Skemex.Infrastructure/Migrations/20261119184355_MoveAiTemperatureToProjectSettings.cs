using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveAiTemperatureToProjectSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AiTemperature",
                table: "project_settings",
                type: "double precision",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0.20000000000000001);

            // Preserve existing workspace temperature per project before dropping the tenant column.
            migrationBuilder.Sql(
                """
                UPDATE project_settings AS ps
                SET "AiTemperature" = t."AiTemperature"
                FROM tenants AS t
                WHERE t."Id" = ps."TenantId";
                """);

            migrationBuilder.DropColumn(
                name: "AiTemperature",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AiTemperature",
                table: "tenants",
                type: "double precision",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0.20000000000000001);

            // Best-effort restore: copy from any project in the tenant.
            migrationBuilder.Sql(
                """
                UPDATE tenants AS t
                SET "AiTemperature" = COALESCE(
                    (
                        SELECT ps."AiTemperature"
                        FROM project_settings AS ps
                        WHERE ps."TenantId" = t."Id"
                        ORDER BY ps."CreatedAt" NULLS LAST
                        LIMIT 1
                    ),
                    0.2);
                """);

            migrationBuilder.DropColumn(
                name: "AiTemperature",
                table: "project_settings");
        }
    }
}
