using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandIntegrationMappingsForEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalDataJson",
                table: "integration_mappings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectUserId",
                table: "integration_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "integration_mappings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TaskAttachmentId",
                table: "integration_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskCommentId",
                table: "integration_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "integration_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskWorkLogId",
                table: "integration_mappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE integration_mappings SET "Provider" = 'Jira' WHERE "Provider" IS NULL OR "Provider" = '';
                UPDATE integration_mappings AS m
                SET "TaskId" = m."LocalEntityId"
                WHERE m."LocalEntityType" = 'Task'
                  AND EXISTS (SELECT 1 FROM project_tasks t WHERE t."Id" = m."LocalEntityId");
                UPDATE integration_mappings AS m
                SET "TaskCommentId" = m."LocalEntityId"
                WHERE m."LocalEntityType" = 'TaskComment'
                  AND EXISTS (SELECT 1 FROM project_task_comments c WHERE c."Id" = m."LocalEntityId");
                UPDATE integration_mappings AS m
                SET "TaskWorkLogId" = m."LocalEntityId"
                WHERE m."LocalEntityType" = 'TaskWorkLog'
                  AND EXISTS (SELECT 1 FROM project_task_work_logs w WHERE w."Id" = m."LocalEntityId");
                UPDATE integration_mappings AS m
                SET "TaskAttachmentId" = m."LocalEntityId"
                WHERE m."LocalEntityType" = 'TaskAttachment'
                  AND EXISTS (SELECT 1 FROM project_task_attachments a WHERE a."Id" = m."LocalEntityId");
                UPDATE integration_mappings AS m
                SET "ProjectUserId" = m."LocalEntityId",
                    "LocalEntityType" = 'ProjectUser'
                WHERE m."LocalEntityType" IN ('User', 'ProjectUser')
                  AND EXISTS (SELECT 1 FROM projects_users pu WHERE pu."Id" = m."LocalEntityId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_ProjectUserId",
                table: "integration_mappings",
                column: "ProjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TaskAttachmentId",
                table: "integration_mappings",
                column: "TaskAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TaskCommentId",
                table: "integration_mappings",
                column: "TaskCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TaskId",
                table: "integration_mappings",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_mappings_TaskWorkLogId",
                table: "integration_mappings",
                column: "TaskWorkLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_project_task_attachments_TaskAttachmen~",
                table: "integration_mappings",
                column: "TaskAttachmentId",
                principalTable: "project_task_attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_project_task_comments_TaskCommentId",
                table: "integration_mappings",
                column: "TaskCommentId",
                principalTable: "project_task_comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_project_task_work_logs_TaskWorkLogId",
                table: "integration_mappings",
                column: "TaskWorkLogId",
                principalTable: "project_task_work_logs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_project_tasks_TaskId",
                table: "integration_mappings",
                column: "TaskId",
                principalTable: "project_tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_integration_mappings_projects_users_ProjectUserId",
                table: "integration_mappings",
                column: "ProjectUserId",
                principalTable: "projects_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_project_task_attachments_TaskAttachmen~",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_project_task_comments_TaskCommentId",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_project_task_work_logs_TaskWorkLogId",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_project_tasks_TaskId",
                table: "integration_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_integration_mappings_projects_users_ProjectUserId",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_ProjectUserId",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TaskAttachmentId",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TaskCommentId",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TaskId",
                table: "integration_mappings");

            migrationBuilder.DropIndex(
                name: "IX_integration_mappings_TaskWorkLogId",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "ExternalDataJson",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "ProjectUserId",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "TaskAttachmentId",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "TaskCommentId",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "integration_mappings");

            migrationBuilder.DropColumn(
                name: "TaskWorkLogId",
                table: "integration_mappings");
        }
    }
}
