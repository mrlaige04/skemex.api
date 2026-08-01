using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Infrastructure.Data;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

[DbContext(typeof(SkemexDbContext))]
[Migration("20260801164000_AddAiModelProviderName")]
public class AddAiModelProviderName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Provider",
            table: "ai_models",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32);

        migrationBuilder.AddColumn<string>(
            name: "ProviderName",
            table: "ai_models",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(
            """
            UPDATE ai_models AS m
            SET "ProviderName" = p."Name"
            FROM ai_providers AS p
            WHERE lower(m."Provider") = lower(p."Key");

            UPDATE ai_models
            SET "ProviderName" = "Provider"
            WHERE "ProviderName" IS NULL OR btrim("ProviderName") = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProviderName",
            table: "ai_models");

        migrationBuilder.AlterColumn<string>(
            name: "Provider",
            table: "ai_models",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64);
    }
}
