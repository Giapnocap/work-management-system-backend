using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Domain.Entities
{
    public class Progress : IHasRowVersion
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
        public ProgressStatus Status { get; set; }
        public decimal HoursSpent { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public TaskItem? Task { get; set; }
        public User? User { get; set; }
    }
}
