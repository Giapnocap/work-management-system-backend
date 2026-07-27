using System;

namespace WorkManagementSystem.Domain.Entities
{
    public class CommentSeen
    {
        public Guid Id { get; set; }
        public Guid CommentId { get; set; }
        public Guid UserId { get; set; }
        public DateTime SeenAt { get; set; } = DateTime.UtcNow;
    }
}
