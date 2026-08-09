using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class CreateTaskDto
    {
        [Required(ErrorMessage = "Tieu de khong duoc de trong!")]
        [MaxLength(200, ErrorMessage = "Tieu de toi da 200 ky tu!")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mo ta toi da 1000 ky tu!")]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<Guid> UserIds { get; set; } = new();
        public List<Guid> UnitIds { get; set; } = new();
        [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Muc uu tien khong hop le.")]
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        public Guid? ProjectId { get; set; }
    }

    public class UpdateTaskDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "RowVersion khong hop le.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required(ErrorMessage = "Tieu de khong duoc de trong!")]
        [MaxLength(200, ErrorMessage = "Tieu de toi da 200 ky tu!")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mo ta toi da 1000 ky tu!")]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Muc uu tien khong hop le.")]
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        public Guid? ProjectId { get; set; }
    }

    public class TaskAssigneeDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
    }

    public class TaskDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<TaskAssigneeDto> Assignees { get; set; } = new();
        public List<UploadFileDto> Files { get; set; } = new();
        public List<SubTaskDto> SubTasks { get; set; } = new();
        public decimal ActualHours { get; set; }
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        public Guid? UnitId { get; set; }
        public string? UnitName { get; set; }
        public string? CreatedByName { get; set; }
        public Guid? ProjectId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public sealed class TaskHistoryDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid ChangedBy { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
