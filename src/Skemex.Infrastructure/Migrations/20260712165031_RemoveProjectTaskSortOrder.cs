using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectTaskSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_tasks_ProjectColumnId_ParentId_SortOrder",
                table: "project_tasks");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "project_tasks");

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_ProjectColumnId_ParentId",
                table: "project_tasks",
                columns: new[] { "ProjectColumnId", "ParentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_tasks_ProjectColumnId_ParentId",
                table: "project_tasks");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "project_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_project_tasks_ProjectColumnId_ParentId_SortOrder",
                table: "project_tasks",
                columns: new[] { "ProjectColumnId", "ParentId", "SortOrder" });
        }
    }
}
