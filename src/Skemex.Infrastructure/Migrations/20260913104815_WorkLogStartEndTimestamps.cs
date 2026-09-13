using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skemex.Infrastructure.Migrations
{
    /// <summary>
    /// Converts WorkDate → StartedAt/EndedAt when the old schema exists.
    /// No-op on fresh databases where work logs are created later with the final schema
    /// (see AddProjectTaskTimeTracking). Timestamp of this migration is before that one.
    /// </summary>
    public partial class WorkLogStartEndTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF to_regclass('public.project_task_work_logs') IS NULL THEN
                        RETURN;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'project_task_work_logs'
                          AND column_name = 'WorkDate'
                    ) THEN
                        RETURN;
                    END IF;

                    DROP INDEX IF EXISTS "IX_project_task_work_logs_TaskId_WorkDate";

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'project_task_work_logs'
                          AND column_name = 'StartedAt'
                    ) THEN
                        ALTER TABLE project_task_work_logs
                            ADD COLUMN "StartedAt" timestamp with time zone NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'project_task_work_logs'
                          AND column_name = 'EndedAt'
                    ) THEN
                        ALTER TABLE project_task_work_logs
                            ADD COLUMN "EndedAt" timestamp with time zone NULL;
                    END IF;

                    UPDATE project_task_work_logs
                    SET
                        "StartedAt" = ("WorkDate"::timestamp AT TIME ZONE 'UTC'),
                        "EndedAt" = ("WorkDate"::timestamp AT TIME ZONE 'UTC')
                            + make_interval(mins => GREATEST("SpentMinutes", 1))
                    WHERE "StartedAt" IS NULL OR "EndedAt" IS NULL;

                    ALTER TABLE project_task_work_logs
                        ALTER COLUMN "StartedAt" SET NOT NULL,
                        ALTER COLUMN "EndedAt" SET NOT NULL;

                    ALTER TABLE project_task_work_logs
                        DROP COLUMN "WorkDate";

                    CREATE INDEX IF NOT EXISTS "IX_project_task_work_logs_TaskId_StartedAt"
                        ON project_task_work_logs ("TaskId", "StartedAt");
                END
                $migration$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                BEGIN
                    IF to_regclass('public.project_task_work_logs') IS NULL THEN
                        RETURN;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'project_task_work_logs'
                          AND column_name = 'StartedAt'
                    ) THEN
                        RETURN;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'project_task_work_logs'
                          AND column_name = 'WorkDate'
                    ) THEN
                        RETURN;
                    END IF;

                    DROP INDEX IF EXISTS "IX_project_task_work_logs_TaskId_StartedAt";

                    ALTER TABLE project_task_work_logs
                        ADD COLUMN "WorkDate" date NULL;

                    UPDATE project_task_work_logs
                    SET "WorkDate" = ("StartedAt" AT TIME ZONE 'UTC')::date
                    WHERE "WorkDate" IS NULL;

                    ALTER TABLE project_task_work_logs
                        ALTER COLUMN "WorkDate" SET NOT NULL;

                    ALTER TABLE project_task_work_logs
                        DROP COLUMN "EndedAt",
                        DROP COLUMN "StartedAt";

                    CREATE INDEX IF NOT EXISTS "IX_project_task_work_logs_TaskId_WorkDate"
                        ON project_task_work_logs ("TaskId", "WorkDate");
                END
                $migration$;
                """);
        }
    }
}
