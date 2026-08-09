namespace WorkManagementSystem.Infrastructure.Storage;

public sealed class UploadCleanupOptions
{
    public const string SectionName = "UploadCleanup";

    public bool Enabled { get; set; } = true;
    public int MinimumAgeHours { get; set; } = 24;
    public int IntervalHours { get; set; } = 24;

    public void Validate()
    {
        if (MinimumAgeHours is < 1 or > 720)
        {
            throw new InvalidOperationException(
                "UploadCleanup:MinimumAgeHours must be between 1 and 720.");
        }

        if (IntervalHours is < 1 or > 168)
        {
            throw new InvalidOperationException(
                "UploadCleanup:IntervalHours must be between 1 and 168.");
        }
    }
}
