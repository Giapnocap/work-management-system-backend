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
        [Required(ErrorMessage = "RowVersion không được để trống.")]
        [MinLength(1, ErrorMessage = "RowVersion không hợp lệ.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required(ErrorMessage = "Vai trò không được để trống.")]
        [RegularExpression("^(User|Manager)$", ErrorMessage = "Vai trò chỉ có thể là User hoặc Manager.")]
        public string Role { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public Guid? OldManagerId { get; set; }
        public string? OldManagerAction { get; set; }
        public Guid? OldManagerNewUnitId { get; set; }
    }
}
