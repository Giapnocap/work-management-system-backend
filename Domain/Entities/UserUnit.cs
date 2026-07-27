namespace WorkManagementSystem.Domain.Entities
{
    public class UserUnit
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid UnitId { get; set; }

        public User? User { get; set; }
        public Unit? Unit { get; set; }
    }
}