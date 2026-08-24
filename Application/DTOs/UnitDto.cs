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
        [Required(ErrorMessage = "Tên phòng ban không được để trống.")]
        [MaxLength(100, ErrorMessage = "Tên phòng ban tối đa 100 ký tự.")]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateUnitDto : CreateUnitDto
    {
        [Required(ErrorMessage = "RowVersion không được để trống.")]
        [MinLength(1, ErrorMessage = "RowVersion không hợp lệ.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
