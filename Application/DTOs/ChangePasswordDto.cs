using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Mat khau cu khong duoc de trong.")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mat khau moi khong duoc de trong.")]
        [MinLength(6, ErrorMessage = "Mat khau moi phai co it nhat 6 ky tu.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xac nhan mat khau khong duoc de trong.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mat khau moi khong khop.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
