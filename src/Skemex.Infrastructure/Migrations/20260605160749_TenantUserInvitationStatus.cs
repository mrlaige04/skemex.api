using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantUserInvitationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvitationToken",
                table: "tenants_users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InvitationTokenExpiresAt",
                table: "tenants_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "tenants_users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql("UPDATE tenants_users SET \"Status\" = 'Active' WHERE \"Status\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_users_InvitationToken",
                table: "tenants_users",
                column: "InvitationToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_users_InvitationToken",
                table: "tenants_users");

            migrationBuilder.DropColumn(
                name: "InvitationToken",
                table: "tenants_users");

            migrationBuilder.DropColumn(
                name: "InvitationTokenExpiresAt",
                table: "tenants_users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "tenants_users");
        }
    }
}
