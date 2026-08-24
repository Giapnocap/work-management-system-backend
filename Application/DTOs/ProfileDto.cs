using System.ComponentModel.DataAnnotations;

public class ProfileDto
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [MaxLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "Số điện thoại tối đa 30 ký tự.")]
    public string? PhoneNumber { get; set; }
}
