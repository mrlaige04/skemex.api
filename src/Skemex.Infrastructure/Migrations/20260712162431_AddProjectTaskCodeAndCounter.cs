using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTaskCodeAndCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "project_tasks",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_task_counters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_task_counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_task_counters_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_task_counters_ProjectId",
                table: "project_task_counters",
                column: "ProjectId",
                unique: true);

            migrationBuilder.Sql(
                """
                WITH numbered_tasks AS (
                    SELECT
                        task."Id",
                        project."Code" AS project_code,
                        ROW_NUMBER() OVER (
                            PARTITION BY task."ProjectId"
                            ORDER BY task."CreatedAt", task."Id"
                        ) AS task_number
                    FROM project_tasks task
                    INNER JOIN projects project ON project."Id" = task."ProjectId"
                )
                UPDATE project_tasks task
                SET "Code" = UPPER(numbered_tasks.project_code) || '_' || numbered_tasks.task_number::text
                FROM numbered_tasks
                WHERE task."Id" = numbered_tasks."Id";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO project_task_counters ("Id", "TenantId", "ProjectId", "NextNumber", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    project."TenantId",
                    project."Id",
                    COALESCE(task_counts.task_count, 0) + 1,
                    NOW() AT TIME ZONE 'UTC'
                FROM projects project
                LEFT JOIN (
                    SELECT "ProjectId", COUNT(*) AS task_count
                    FROM project_tasks
                    GROUP BY "ProjectId"
                ) task_counts ON task_counts."ProjectId" = project."Id";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "project_tasks",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_ProjectId_Code",
                table: "project_tasks",
                columns: new[] { "ProjectId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_task_counters");

            migrationBuilder.DropIndex(
                name: "IX_project_tasks_ProjectId_Code",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "project_tasks");
        }
    }
}
