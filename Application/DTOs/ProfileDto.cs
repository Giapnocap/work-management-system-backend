using System.ComponentModel.DataAnnotations;

public class ProfileDto
{
    [Required(ErrorMessage = "Ho ten khong duoc de trong.")]
    [MaxLength(150, ErrorMessage = "Ho ten toi da 150 ky tu.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email khong dung dinh dang.")]
    [MaxLength(256, ErrorMessage = "Email toi da 256 ky tu.")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "So dien thoai toi da 30 ky tu.")]
    public string? PhoneNumber { get; set; }
}
