namespace WorkManagementSystem.Application.Common;

public sealed class WorkloadOptions
{
    public const string SectionName = "Workload";

    public decimal DefaultWeeklyCapacityHours { get; set; } = 40m;
    public decimal BusyThresholdPercent { get; set; } = 70m;
    public decimal OverloadedThresholdPercent { get; set; } = 90m;
    public int MaxRangeDays { get; set; } = 366;

    public bool IsValid()
    {
        return DefaultWeeklyCapacityHours is > 0m and <= 168m &&
               BusyThresholdPercent is > 0m and < 100m &&
               OverloadedThresholdPercent is > 0m and <= 100m &&
               BusyThresholdPercent < OverloadedThresholdPercent &&
               MaxRangeDays is > 0 and <= 3660;
    }
}
