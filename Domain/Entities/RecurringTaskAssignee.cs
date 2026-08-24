namespace WorkManagementSystem.Domain.Entities;

public sealed class RecurringTaskAssignee
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid UserId { get; set; }

    public RecurringTaskTemplate? Template { get; set; }
    public User? User { get; set; }
}
