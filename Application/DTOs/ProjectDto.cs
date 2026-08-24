using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid? UnitId { get; set; }
        public string? UnitName { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public List<ProjectStatusCountDto> StatusCounts { get; set; } = new();
    }

    public class CreateProjectDto
    {
        [Required(ErrorMessage = "Tên dự án không được để trống.")]
        [MaxLength(200, ErrorMessage = "Tên dự án tối đa 200 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mô tả dự án tối đa 1000 ký tự.")]
        public string Description { get; set; } = string.Empty;

        [NotEmptyGuid(ErrorMessage = "UnitId không được rỗng.")]
        public Guid? UnitId { get; set; }
    }

    public class UpdateProjectDto : CreateProjectDto
    {
        [Required(ErrorMessage = "RowVersion không được để trống.")]
        [MinLength(1, ErrorMessage = "RowVersion không hợp lệ.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class ProjectStatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
