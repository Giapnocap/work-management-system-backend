using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.DTOs
{
    public class AuthDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống!")]
        [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự!")]
        [MaxLength(100, ErrorMessage = "Tên đăng nhập tối đa 100 ký tự.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống!")]
        [MaxLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống!")]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        [MaxLength(72, ErrorMessage = "Mật khẩu tối đa 72 ký tự.")]
        [PasswordPolicy]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = SystemRoles.User;
        public Guid? UnitId { get; set; }
        [MaxLength(30, ErrorMessage = "Số điện thoại tối đa 30 ký tự.")]
        public string? PhoneNumber { get; set; }
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống!")]
        [MaxLength(100, ErrorMessage = "Tên đăng nhập tối đa 100 ký tự.")]
        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống!")]
        [MaxLength(72, ErrorMessage = "Mật khẩu tối đa 72 ký tự.")]
        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [MaxLength(100, ErrorMessage = "Tên đăng nhập tối đa 100 ký tự.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
        [MaxLength(72, ErrorMessage = "Mật khẩu mới tối đa 72 ký tự.")]
        [PasswordPolicy]
        public string NewPassword { get; set; } = string.Empty;
    }
}
