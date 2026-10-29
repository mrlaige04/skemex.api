using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraIntegrationConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CloudId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SiteUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SiteName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExternalProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExternalProjectKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalProjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedRefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Scopes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConnectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_connections_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalEntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalEntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalEntityKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_mappings_integration_connections_IntegrationCon~",
                        column: x => x.IntegrationConnectionId,
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_integration_mappings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_ProjectId",
                table: "integration_connections",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_TenantId_ProjectId_Provider",
                table: "integration_connections",
                columns: new[] { "TenantId", "ProjectId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_IntegrationConnectionId",
                table: "integration_mappings",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_ProjectId",
                table: "integration_mappings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TenantId_IntegrationConnectionId_Exter~",
                table: "integration_mappings",
                columns: new[] { "TenantId", "IntegrationConnectionId", "ExternalEntityType", "ExternalEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TenantId_IntegrationConnectionId_Local~",
                table: "integration_mappings",
                columns: new[] { "TenantId", "IntegrationConnectionId", "LocalEntityType", "LocalEntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_mappings");

            migrationBuilder.DropTable(
                name: "integration_connections");
        }
    }
}
