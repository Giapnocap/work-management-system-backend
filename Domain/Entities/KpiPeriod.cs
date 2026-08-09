namespace WorkManagementSystem.Domain.Entities
{
    public class KpiPeriod : IHasRowVersion
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Monthly";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Open";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LockedAt { get; set; }
        public Guid? LockedBy { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public User? Locker { get; set; }
    }
}
