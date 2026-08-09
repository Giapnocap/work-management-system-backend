using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.DTOs
{
    public class AuthDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống!")]
        [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự!")]
        [MaxLength(100, ErrorMessage = "Ten dang nhap toi da 100 ky tu.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống!")]
        [MaxLength(150, ErrorMessage = "Ho ten toi da 150 ky tu.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống!")]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mat khau phai co it nhat 8 ky tu.")]
        [MaxLength(72, ErrorMessage = "Mat khau toi da 72 ky tu.")]
        [PasswordPolicy]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = SystemRoles.User;
        public Guid? UnitId { get; set; }
        [MaxLength(30, ErrorMessage = "So dien thoai toi da 30 ky tu.")]
        public string? PhoneNumber { get; set; }
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống!")]
        [MaxLength(100, ErrorMessage = "Ten dang nhap toi da 100 ky tu.")]
        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống!")]
        [MaxLength(72, ErrorMessage = "Mat khau toi da 72 ky tu.")]
        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        [Required]
        [MaxLength(100, ErrorMessage = "Ten dang nhap toi da 100 ky tu.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mat khau moi phai co it nhat 8 ky tu.")]
        [MaxLength(72, ErrorMessage = "Mat khau moi toi da 72 ky tu.")]
        [PasswordPolicy]
        public string NewPassword { get; set; } = string.Empty;
    }
}
