namespace Skemex.Domain.Consts;

public static class ProjectTaskTimeLimits
{
    public const int MaxEstimateMinutes = 100_000; // ~1666h
    public const int MaxSpentMinutesPerEntry = 24 * 60 * 14; // 2 weeks
    public const int MaxCommentLength = 2000;
    public const decimal MaxStoryPoints = 1000m;

    public static decimal MaxEstimateHours => MaxEstimateMinutes / 60m;
}
