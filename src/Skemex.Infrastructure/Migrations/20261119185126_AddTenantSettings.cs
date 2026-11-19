using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AiSettings_MaxTreeDepth = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    AiSettings_MaxNodes = table.Column<int>(type: "integer", nullable: false, defaultValue: 16),
                    AiSettings_Temperature = table.Column<double>(type: "double precision", precision: 3, scale: 2, nullable: false, defaultValue: 0.20000000000000001),
                    AiSettings_DefaultModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiSettings_EnrichmentModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_settings_ai_models_AiSettings_DefaultModelId",
                        column: x => x.AiSettings_DefaultModelId,
                        principalTable: "ai_models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_settings_ai_models_AiSettings_EnrichmentModelId",
                        column: x => x.AiSettings_EnrichmentModelId,
                        principalTable: "ai_models",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_AiSettings_DefaultModelId",
                table: "tenant_settings",
                column: "AiSettings_DefaultModelId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_AiSettings_EnrichmentModelId",
                table: "tenant_settings",
                column: "AiSettings_EnrichmentModelId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_settings_TenantId",
                table: "tenant_settings",
                column: "TenantId",
                unique: true);

            // Backfill existing tenants with defaults + first active catalog model (if any).
            migrationBuilder.Sql(
                """
                INSERT INTO tenant_settings (
                    "Id",
                    "TenantId",
                    "CreatedAt",
                    "UpdatedAt",
                    "AiSettings_MaxTreeDepth",
                    "AiSettings_MaxNodes",
                    "AiSettings_Temperature",
                    "AiSettings_DefaultModelId",
                    "AiSettings_EnrichmentModelId")
                SELECT
                    gen_random_uuid(),
                    t."Id",
                    NOW() AT TIME ZONE 'utc',
                    NULL,
                    2,
                    16,
                    0.2,
                    (
                        SELECT m."Id"
                        FROM ai_models AS m
                        WHERE m."IsActive" = TRUE
                        ORDER BY m."DisplayName", m."ExternalId"
                        LIMIT 1
                    ),
                    (
                        SELECT m."Id"
                        FROM ai_models AS m
                        WHERE m."IsActive" = TRUE
                        ORDER BY m."DisplayName", m."ExternalId"
                        LIMIT 1
                    )
                FROM tenants AS t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM tenant_settings AS ts
                    WHERE ts."TenantId" = t."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_settings");
        }
    }
}
