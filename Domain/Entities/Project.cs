namespace WorkManagementSystem.Domain.Entities
{
    public class Project : IHasRowVersion
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; } = false;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Unit? Unit { get; set; }
        public User? Creator { get; set; }
    }
}
