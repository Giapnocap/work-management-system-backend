namespace WorkManagementSystem.Domain.Entities
{
    public sealed class AuditLog
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public Guid? ActorUserId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string? DetailsJson { get; set; }

        public User? ActorUser { get; set; }
    }
}
