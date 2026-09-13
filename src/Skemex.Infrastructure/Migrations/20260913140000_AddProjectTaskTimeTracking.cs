using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260913140000_AddProjectTaskTimeTracking")]
public class AddProjectTaskTimeTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OriginalEstimateMinutes",
            table: "project_tasks",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RemainingEstimateMinutes",
            table: "project_tasks",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "StoryPoints",
            table: "project_tasks",
            type: "numeric(8,2)",
            precision: 8,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SpentMinutes",
            table: "project_tasks",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Final work-log schema (StartedAt/EndedAt). WorkLogStartEndTimestamps is a no-op
        // when this table does not exist yet because its migration id sorts earlier.
        migrationBuilder.CreateTable(
            name: "project_task_work_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                SpentMinutes = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_task_work_logs", x => x.Id);
                table.ForeignKey(
                    name: "FK_project_task_work_logs_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_project_task_work_logs_project_tasks_TaskId",
                    column: x => x.TaskId,
                    principalTable: "project_tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_project_task_work_logs_projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_project_task_work_logs_ProjectId_UserId",
            table: "project_task_work_logs",
            columns: new[] { "ProjectId", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_project_task_work_logs_TaskId_StartedAt",
            table: "project_task_work_logs",
            columns: new[] { "TaskId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_project_task_work_logs_UserId",
            table: "project_task_work_logs",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "project_task_work_logs");

        migrationBuilder.DropColumn(name: "OriginalEstimateMinutes", table: "project_tasks");
        migrationBuilder.DropColumn(name: "RemainingEstimateMinutes", table: "project_tasks");
        migrationBuilder.DropColumn(name: "StoryPoints", table: "project_tasks");
        migrationBuilder.DropColumn(name: "SpentMinutes", table: "project_tasks");
    }
}
