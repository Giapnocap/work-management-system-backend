using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mật khẩu cũ không được để trống.")]
        [MaxLength(72, ErrorMessage = "Mật khẩu cũ tối đa 72 ký tự.")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
        [MaxLength(72, ErrorMessage = "Mật khẩu mới tối đa 72 ký tự.")]
        [PasswordPolicy]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống.")]
        [MaxLength(72, ErrorMessage = "Xác nhận mật khẩu tối đa 72 ký tự.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu mới không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
