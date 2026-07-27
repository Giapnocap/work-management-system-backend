namespace WorkManagementSystem.Application.DTOs
{
    public sealed class AuditLogDto
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public Guid? ActorUserId { get; set; }
        public DateTime OccurredAt { get; set; }
        public string? DetailsJson { get; set; }
    }

    public sealed class AuditLogPageDto
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
        public List<AuditLogDto> Data { get; set; } = new();
    }
}
