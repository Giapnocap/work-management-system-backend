using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class UnitDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class CreateUnitDto
    {
        [Required(ErrorMessage = "Ten phong ban khong duoc de trong.")]
        [MaxLength(100, ErrorMessage = "Ten phong ban toi da 100 ky tu.")]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateUnitDto : CreateUnitDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "RowVersion khong hop le.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
