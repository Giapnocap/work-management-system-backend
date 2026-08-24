namespace WorkManagementSystem.Domain.Entities;

public sealed class GeneratedTaskOccurrence
{
    public Guid TemplateId { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public Guid TaskId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }

    public RecurringTaskTemplate? Template { get; set; }
    public TaskItem? Task { get; set; }
}
