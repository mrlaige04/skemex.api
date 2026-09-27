using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HybridRagChunkEnrichmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnglishText",
                table: "project_document_chunks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Keywords",
                table: "project_document_chunks",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.CreateIndex(
                name: "IX_project_document_chunks_Keywords",
                table: "project_document_chunks",
                column: "Keywords")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_document_chunks_Keywords",
                table: "project_document_chunks");

            migrationBuilder.DropColumn(
                name: "EnglishText",
                table: "project_document_chunks");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "project_document_chunks");
        }
    }
}
