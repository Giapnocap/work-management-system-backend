namespace WorkManagementSystem.Application.Common;

public sealed class DeadlineReminderOptions
{
    public const string SectionName = "DeadlineReminders";
    public const int MaxPolicyHours = 720;

    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 60;
    public int MaxTasksPerBatch { get; set; } = 100;
    public int MaxDeliveriesPerBatch { get; set; } = 200;
    public int MaxRetryCount { get; set; } = 3;

    public bool IsValid()
    {
        return PollingIntervalSeconds is >= 5 and <= 86400 &&
               MaxTasksPerBatch is >= 1 and <= 1000 &&
               MaxDeliveriesPerBatch is >= 1 and <= 2000 &&
               MaxRetryCount is >= 1 and <= 20;
    }
}
