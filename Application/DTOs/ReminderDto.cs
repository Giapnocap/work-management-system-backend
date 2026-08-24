using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.DTOs;

public sealed class UpsertReminderPolicyDto
{
    [Required(ErrorMessage = "Phạm vi chính sách không được để trống.")]
    [RegularExpression(
        "^(Global|Unit|Project)$",
        ErrorMessage = "Phạm vi chính sách phải là Global, Unit hoặc Project.")]
    public string ScopeType { get; set; } = string.Empty;

    public Guid? ScopeId { get; set; }

    [Range(
        1,
        DeadlineReminderOptions.MaxPolicyHours,
        ErrorMessage = "Mốc nhắc trước hạn phải từ 1 đến 720 giờ.")]
    public int BeforeDueHours { get; set; } = 24;

    [Range(
        0,
        DeadlineReminderOptions.MaxPolicyHours,
        ErrorMessage = "Mốc cảnh báo quá hạn phải từ 0 đến 720 giờ.")]
    public int OverdueEscalationHours { get; set; } = 24;

    public bool NotifyAssignee { get; set; } = true;
    public bool NotifyManager { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class ReminderPolicyDto
{
    public Guid Id { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid? ScopeId { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public int BeforeDueHours { get; set; }
    public int OverdueEscalationHours { get; set; }
    public bool NotifyAssignee { get; set; }
    public bool NotifyManager { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class ScheduledNotificationDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledForUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
