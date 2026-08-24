using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class CreateProgressDto
    {
        [Required(ErrorMessage = "TaskId không được để trống.")]
        [NotEmptyGuid(ErrorMessage = "TaskId không được rỗng.")]
        public Guid TaskId { get; set; }

        [Range(0, 100, ErrorMessage = "Phần trăm hoàn thành phải từ 0 đến 100!")]
        public int Percent { get; set; }

        [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự!")]
        public string Description { get; set; } = string.Empty;

        public Guid? FileId { get; set; }
        public decimal HoursSpent { get; set; } = 0;
        public bool? SubmitForReview { get; set; }
    }

    public class ProgressDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmployeeCode { get; set; } = string.Empty;
        public int Percent { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public List<UploadFileDto> Files { get; set; } = new();
        public decimal HoursSpent { get; set; }
        public string? ReviewComment { get; set; }
        public string? UnitName { get; set; }
        public bool RequiresReview { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
