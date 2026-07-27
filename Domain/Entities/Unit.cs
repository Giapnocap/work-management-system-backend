namespace WorkManagementSystem.Domain.Entities
{
    public class Unit
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
