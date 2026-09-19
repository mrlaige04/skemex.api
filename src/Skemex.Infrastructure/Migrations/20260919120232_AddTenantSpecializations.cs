using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSpecializations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Skills",
                table: "tenants_users",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "tenant_specializations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DefaultSkills = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_specializations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_specializations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenants_users_specializations",
                columns: table => new
                {
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSpecializationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants_users_specializations", x => new { x.TenantUserId, x.TenantSpecializationId });
                    table.ForeignKey(
                        name: "FK_tenants_users_specializations_tenant_specializations_Tenant~",
                        column: x => x.TenantSpecializationId,
                        principalTable: "tenant_specializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenants_users_specializations_tenants_users_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "tenants_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_specializations_TenantId_Title",
                table: "tenant_specializations",
                columns: new[] { "TenantId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_users_specializations_TenantSpecializationId",
                table: "tenants_users_specializations",
                column: "TenantSpecializationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants_users_specializations");

            migrationBuilder.DropTable(
                name: "tenant_specializations");

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "tenants_users");
        }
    }
}
