using ErrorOr;

namespace Skemex.Application.Services.Projects;

public static class ProjectTaskTimeTracking
{
    public const int MaxEstimateMinutes = 100_000; // ~1666h
    public const int MaxSpentMinutesPerEntry = 24 * 60 * 14; // 2 weeks
    public const int MaxCommentLength = 2000;
    public const decimal MaxStoryPoints = 1000m;

    public static ErrorOr<int> CalculateSpentMinutes(DateTime startedAt, DateTime endedAt)
    {
        if (endedAt <= startedAt)
        {
            return Error.Validation(
                "WorkLog.InvalidRange",
                "End time must be after start time.");
        }

        var minutes = (int)Math.Ceiling((endedAt - startedAt).TotalMinutes);
        if (minutes < 1)
        {
            minutes = 1;
        }

        if (minutes > MaxSpentMinutesPerEntry)
        {
            return Error.Validation(
                "WorkLog.InvalidSpentMinutes",
                $"Spent time must be at most {MaxSpentMinutesPerEntry} minutes.");
        }

        return minutes;
    }

    /// <summary>
    /// Sets original estimate and always recalculates remaining as estimate − spent.
    /// </summary>
    public static void ApplyOriginalEstimate(Domain.Entities.Projects.ProjectTask task, int? minutes)
    {
        task.OriginalEstimateMinutes = minutes;
        RecalculateRemaining(task);
    }

    /// <summary>
    /// Remaining = max(0, original estimate − spent). Cleared when there is no estimate.
    /// </summary>
    public static void RecalculateRemaining(Domain.Entities.Projects.ProjectTask task)
    {
        if (task.OriginalEstimateMinutes is null)
        {
            task.RemainingEstimateMinutes = null;
            return;
        }

        task.RemainingEstimateMinutes = Math.Max(0, task.OriginalEstimateMinutes.Value - task.SpentMinutes);
    }

    public static int? HoursToMinutes(decimal? hours)
    {
        if (hours is null)
        {
            return null;
        }

        if (hours <= 0)
        {
            return null;
        }

        var minutes = (int)Math.Round(hours.Value * 60m, MidpointRounding.AwayFromZero);
        if (minutes < 1)
        {
            minutes = 1;
        }

        return Math.Min(minutes, MaxEstimateMinutes);
    }

    public static decimal MaxEstimateHours => MaxEstimateMinutes / 60m;

    /// <summary>
    /// Rejects ranges that overlap any other work log on the same task by more than 1 minute.
    /// </summary>
    public static ErrorOr<Success> EnsureNoSignificantOverlap(
        DateTime startedAt,
        DateTime endedAt,
        IEnumerable<Domain.Entities.Projects.ProjectTaskWorkLog> existingLogs,
        Guid? excludeWorkLogId = null)
    {
        foreach (var existing in existingLogs)
        {
            if (excludeWorkLogId is not null && existing.Id == excludeWorkLogId.Value)
            {
                continue;
            }

            var overlapMinutes = OverlapMinutes(startedAt, endedAt, existing.StartedAt, existing.EndedAt);
            if (overlapMinutes > 1)
            {
                return Error.Validation(
                    "WorkLog.Overlap",
                    "This time range overlaps an existing work log on this task by more than 1 minute.");
            }
        }

        return Result.Success;
    }

    private static double OverlapMinutes(
        DateTime aStart,
        DateTime aEnd,
        DateTime bStart,
        DateTime bEnd)
    {
        var overlapStart = aStart > bStart ? aStart : bStart;
        var overlapEnd = aEnd < bEnd ? aEnd : bEnd;
        if (overlapEnd <= overlapStart)
        {
            return 0;
        }

        return (overlapEnd - overlapStart).TotalMinutes;
    }
}
