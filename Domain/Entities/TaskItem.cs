using TaskStatusEnum = WorkManagementSystem.Domain.Enums.TaskStatus;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Domain.Entities
{
    public class TaskItem : IHasRowVersion
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public TaskStatusEnum Status { get; set; } = TaskStatusEnum.NotStarted;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public bool RequiresReview { get; set; } = true;
        public decimal ActualHours { get; set; } = 0;
        public Guid? UnitId { get; set; }
        public Guid? ProjectId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public User? Creator { get; set; }
        public Unit? Unit { get; set; }
        public Project? Project { get; set; }
    }
}
