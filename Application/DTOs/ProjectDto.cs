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
        [Required(ErrorMessage = "Ten project khong duoc de trong.")]
        [MaxLength(200, ErrorMessage = "Ten project toi da 200 ky tu.")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mo ta project toi da 1000 ky tu.")]
        public string Description { get; set; } = string.Empty;

        [NotEmptyGuid(ErrorMessage = "UnitId khong duoc rong.")]
        public Guid? UnitId { get; set; }
    }

    public class UpdateProjectDto : CreateProjectDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "RowVersion khong hop le.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class ProjectStatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
