namespace WorkManagementSystem.Domain.Entities;

public sealed class TaskDependency
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
