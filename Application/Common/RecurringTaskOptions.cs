namespace WorkManagementSystem.Application.Common;

public sealed class RecurringTaskOptions
{
    public const string SectionName = "RecurringTasks";

    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 60;
    public int MaxTemplatesPerBatch { get; set; } = 50;
    public int MaxCatchUpOccurrencesPerTemplate { get; set; } = 10;

    public bool IsValid()
    {
        return PollingIntervalSeconds is >= 5 and <= 86400 &&
               MaxTemplatesPerBatch is >= 1 and <= 500 &&
               MaxCatchUpOccurrencesPerTemplate is >= 1 and <= 100;
    }
}
