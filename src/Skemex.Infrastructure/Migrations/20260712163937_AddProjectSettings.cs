using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultTaskColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_settings_project_columns_DefaultTaskColumnId",
                        column: x => x.DefaultTaskColumnId,
                        principalTable: "project_columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_settings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_settings_DefaultTaskColumnId",
                table: "project_settings",
                column: "DefaultTaskColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_project_settings_ProjectId",
                table: "project_settings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO project_settings ("Id", "TenantId", "ProjectId", "DefaultTaskColumnId", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    project."TenantId",
                    project."Id",
                    first_column."Id",
                    NOW() AT TIME ZONE 'UTC'
                FROM projects project
                INNER JOIN LATERAL (
                    SELECT column_row."Id"
                    FROM project_columns column_row
                    WHERE column_row."ProjectId" = project."Id"
                    ORDER BY column_row."SortOrder", column_row."Title"
                    LIMIT 1
                ) first_column ON TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_settings");
        }
    }
}
