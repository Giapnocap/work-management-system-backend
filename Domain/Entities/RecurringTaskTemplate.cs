using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Domain.Entities;

public sealed class RecurringTaskTemplate : IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public bool RequiresReview { get; set; } = true;
    public decimal? PlannedEffortHours { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public int Interval { get; set; } = 1;
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime NextRunAtUtc { get; set; }
    public DateTime? LastGeneratedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Unit? Unit { get; set; }
    public Project? Project { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<RecurringTaskAssignee> Assignees { get; set; } = new List<RecurringTaskAssignee>();
    public ICollection<GeneratedTaskOccurrence> Occurrences { get; set; } = new List<GeneratedTaskOccurrence>();
}
