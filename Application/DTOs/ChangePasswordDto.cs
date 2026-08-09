using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Common;

namespace WorkManagementSystem.Application.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mat khau cu khong duoc de trong.")]
        [MaxLength(72, ErrorMessage = "Mat khau cu toi da 72 ky tu.")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mat khau moi khong duoc de trong.")]
        [MinLength(PasswordPolicy.MinimumLength, ErrorMessage = "Mat khau moi phai co it nhat 8 ky tu.")]
        [MaxLength(72, ErrorMessage = "Mat khau moi toi da 72 ky tu.")]
        [PasswordPolicy]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xac nhan mat khau khong duoc de trong.")]
        [MaxLength(72, ErrorMessage = "Xac nhan mat khau toi da 72 ky tu.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mat khau moi khong khop.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
