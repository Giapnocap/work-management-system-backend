namespace WorkManagementSystem.Domain.Entities
{
    public class UserWorkHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? UnitId { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public Guid? ChangedBy { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Unit? Unit { get; set; }
        public User? ChangedByUser { get; set; }
    }
}
