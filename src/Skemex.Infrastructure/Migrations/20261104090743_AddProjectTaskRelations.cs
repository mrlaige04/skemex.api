using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTaskRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_task_relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_task_relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_task_relations_project_tasks_SourceTaskId",
                        column: x => x.SourceTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_task_relations_project_tasks_TargetTaskId",
                        column: x => x.TargetTaskId,
                        principalTable: "project_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_task_relations_SourceTaskId_TargetTaskId_RelationTy~",
                table: "project_task_relations",
                columns: new[] { "SourceTaskId", "TargetTaskId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_task_relations_TargetTaskId",
                table: "project_task_relations",
                column: "TargetTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_project_task_relations_TenantId_SourceTaskId",
                table: "project_task_relations",
                columns: new[] { "TenantId", "SourceTaskId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_task_relations");
        }
    }
}
