using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260913120000_AddProjectTaskAcceptanceCriteriaAndTestCases")]
public class AddProjectTaskAcceptanceCriteriaAndTestCases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AcceptanceCriteria",
            table: "project_tasks",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "TestCases",
            table: "project_tasks",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptanceCriteria",
            table: "project_tasks");

        migrationBuilder.DropColumn(
            name: "TestCases",
            table: "project_tasks");
    }
}
