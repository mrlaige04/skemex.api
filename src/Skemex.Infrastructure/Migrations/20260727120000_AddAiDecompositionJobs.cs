using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260727120000_AddAiDecompositionJobs")]
public class AddAiDecompositionJobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ai_decomposition_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserInput = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                CustomInstructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                RootTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                HangfireJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_decomposition_jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ai_decomposition_jobs_project_tasks_RootTaskId",
                    column: x => x.RootTaskId,
                    principalTable: "project_tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ai_decomposition_jobs_projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ai_decomposition_jobs_users_RequestedByUserId",
                    column: x => x.RequestedByUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ai_decomposition_jobs_ProjectId_CreatedAt",
            table: "ai_decomposition_jobs",
            columns: new[] { "ProjectId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ai_decomposition_jobs_RequestedByUserId",
            table: "ai_decomposition_jobs",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ai_decomposition_jobs_RootTaskId",
            table: "ai_decomposition_jobs",
            column: "RootTaskId");

        migrationBuilder.CreateIndex(
            name: "IX_ai_decomposition_jobs_TenantId_ProjectId",
            table: "ai_decomposition_jobs",
            columns: new[] { "TenantId", "ProjectId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ai_decomposition_jobs");
    }
}
