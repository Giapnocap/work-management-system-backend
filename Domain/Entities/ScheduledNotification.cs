using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Domain.Entities;

public sealed class ScheduledNotification : IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public ScheduledNotificationType Type { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public ScheduledNotificationStatus Status { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public TaskItem? Task { get; set; }
}
