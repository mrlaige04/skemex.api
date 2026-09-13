using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkLogStartEndTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_task_work_logs_TaskId_WorkDate",
                table: "project_task_work_logs");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "project_task_work_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "project_task_work_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE project_task_work_logs
                SET
                    "StartedAt" = ("WorkDate"::timestamp AT TIME ZONE 'UTC'),
                    "EndedAt" = ("WorkDate"::timestamp AT TIME ZONE 'UTC')
                        + make_interval(mins => GREATEST("SpentMinutes", 1))
                WHERE "StartedAt" IS NULL OR "EndedAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAt",
                table: "project_task_work_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndedAt",
                table: "project_task_work_logs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "project_task_work_logs");

            migrationBuilder.CreateIndex(
                name: "IX_project_task_work_logs_TaskId_StartedAt",
                table: "project_task_work_logs",
                columns: new[] { "TaskId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_task_work_logs_TaskId_StartedAt",
                table: "project_task_work_logs");

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "project_task_work_logs",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE project_task_work_logs
                SET "WorkDate" = ("StartedAt" AT TIME ZONE 'UTC')::date
                WHERE "WorkDate" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "WorkDate",
                table: "project_task_work_logs",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "project_task_work_logs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "project_task_work_logs");

            migrationBuilder.CreateIndex(
                name: "IX_project_task_work_logs_TaskId_WorkDate",
                table: "project_task_work_logs",
                columns: new[] { "TaskId", "WorkDate" });
        }
    }
}
