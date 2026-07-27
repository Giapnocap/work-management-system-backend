namespace WorkManagementSystem.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public Guid? UnitId { get; set; }
        public DateTime JoinedUnitAt { get; set; } = DateTime.UtcNow;
        public bool IsApproved { get; set; } = false;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsDeleted { get; set; } = false;
        public int TokenVersion { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public Unit? Unit { get; set; }
        public ICollection<UserUnit> UserUnits { get; set; } = new List<UserUnit>();

        public void InvalidateSessions()
        {
            TokenVersion = checked(TokenVersion + 1);
        }
    }
}
