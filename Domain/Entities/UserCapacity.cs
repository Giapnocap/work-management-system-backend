namespace WorkManagementSystem.Domain.Entities
{
    public sealed class UserCapacity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal WeeklyCapacityHours { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid ChangedByUserId { get; set; }

        public User? User { get; set; }
        public User? ChangedByUser { get; set; }
    }
}
