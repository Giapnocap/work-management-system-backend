using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public bool IsApproved { get; set; }
        public string? PhoneNumber { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class UpdateUserDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "RowVersion khong hop le.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required]
        [RegularExpression("^(User|Manager)$", ErrorMessage = "Role chi co the la User hoac Manager.")]
        public string Role { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public Guid? OldManagerId { get; set; }
        public string? OldManagerAction { get; set; }
        public Guid? OldManagerNewUnitId { get; set; }
    }
}
