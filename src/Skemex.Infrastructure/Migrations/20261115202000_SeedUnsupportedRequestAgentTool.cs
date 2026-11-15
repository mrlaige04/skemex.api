using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Skemex.Domain.Consts;

#nullable disable

namespace Skemex.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeedUnsupportedRequestAgentTool : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "agent_tools",
            columns: new[] { "Id", "CreatedAt", "Description", "SystemName", "SystemPrompt", "UpdatedAt" },
            values: new object[]
            {
                UnsupportedRequestToolDefaults.SeedId,
                UnsupportedRequestToolDefaults.SeedTimestamp,
                UnsupportedRequestToolDefaults.Description,
                UnsupportedRequestToolDefaults.SystemName,
                UnsupportedRequestToolDefaults.SystemPrompt,
                UnsupportedRequestToolDefaults.SeedTimestamp,
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "agent_tools",
            keyColumn: "Id",
            keyValue: UnsupportedRequestToolDefaults.SeedId);
    }
}
