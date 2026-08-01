using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260801130000_AddProjectSettingsAiTreeLimits")]
public class AddProjectSettingsAiTreeLimits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AiMaxTreeDepth",
            table: "project_settings",
            type: "integer",
            nullable: false,
            defaultValue: 2);

        migrationBuilder.AddColumn<int>(
            name: "AiMaxNodes",
            table: "project_settings",
            type: "integer",
            nullable: false,
            defaultValue: 16);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AiMaxTreeDepth",
            table: "project_settings");

        migrationBuilder.DropColumn(
            name: "AiMaxNodes",
            table: "project_settings");
    }
}
