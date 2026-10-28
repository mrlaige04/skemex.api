using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgentJobArtefactsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtefactsJson",
                table: "ai_agent_jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtefactsJson",
                table: "ai_agent_jobs");
        }
    }
}
