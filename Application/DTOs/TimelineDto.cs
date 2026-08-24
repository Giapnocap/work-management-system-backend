using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs;

public static class TimelineEventTypes
{
    public const string TaskCreated = "task.created";
    public const string TaskUpdated = "task.updated";
    public const string TaskStatusChanged = "task.status_changed";
    public const string AssignmentAdded = "assignment.added";
    public const string ProgressReported = "progress.reported";
    public const string ProgressStatusChanged = "progress.status_changed";
    public const string ReviewCompleted = "review.completed";
    public const string CommentAdded = "comment.added";
    public const string FileUploaded = "file.uploaded";
    public const string ReminderSent = "reminder.sent";
    public const string EscalationSent = "escalation.sent";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        TaskCreated,
        TaskUpdated,
        TaskStatusChanged,
        AssignmentAdded,
        ProgressReported,
        ProgressStatusChanged,
        ReviewCompleted,
        CommentAdded,
        FileUploaded,
        ReminderSent,
        EscalationSent
    };
}

public sealed class TaskTimelineQueryDto
{
    public string? Cursor { get; set; }

    [Range(1, 100)]
    public int Size { get; set; } = 20;

    public string? Type { get; set; }
    public Guid? ActorId { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
}

public sealed class TimelinePageDto
{
    public IReadOnlyList<TimelineItemDto> Items { get; init; } = Array.Empty<TimelineItemDto>();
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed class TimelineItemDto
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Type { get; init; } = string.Empty;
    public TimelineActorDto Actor { get; init; } = new();
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid RelatedEntityId { get; init; }
    public TimelineMetadataDto? Metadata { get; init; }
}

public sealed class TimelineActorDto
{
    public Guid? Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? EmployeeCode { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class TimelineMetadataDto
{
    public string? FieldName { get; init; }
    public string? PreviousValue { get; init; }
    public string? CurrentValue { get; init; }
    public string? Reason { get; init; }
    public string? Status { get; init; }
    public int? Percent { get; init; }
    public decimal? HoursSpent { get; init; }
    public bool? IsApproved { get; init; }
    public string? FileName { get; init; }
    public string? AssignmentTargetType { get; init; }
    public Guid? AssignmentTargetId { get; init; }
    public string? AssignmentTargetName { get; init; }
    public string? ReminderType { get; init; }
    public bool IsDeleted { get; init; }
}
