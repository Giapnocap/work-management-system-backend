using System.ComponentModel.DataAnnotations;

namespace WorkManagementSystem.Application.DTOs
{
    public class CreateTaskDto
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống!")]
        [MaxLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự!")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự!")]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public List<Guid> UserIds { get; set; } = new();
        public List<Guid> UnitIds { get; set; } = new();
        [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Mức ưu tiên không hợp lệ.")]
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        [Range(0.01, 100000.0, ErrorMessage = "Khối lượng kế hoạch phải lớn hơn 0.")]
        public decimal? PlannedEffortHours { get; set; }
        public Guid? ProjectId { get; set; }
    }

    public class UpdateTaskDto
    {
        [Required(ErrorMessage = "RowVersion không được để trống.")]
        [MinLength(1, ErrorMessage = "RowVersion không hợp lệ.")]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        [Required(ErrorMessage = "Tiêu đề không được để trống!")]
        [MaxLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự!")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự!")]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        [RegularExpression("^(Low|Medium|High|Urgent)$", ErrorMessage = "Mức ưu tiên không hợp lệ.")]
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        [Range(0.01, 100000.0, ErrorMessage = "Khối lượng kế hoạch phải lớn hơn 0.")]
        public decimal? PlannedEffortHours { get; set; }
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
        public decimal? PlannedEffortHours { get; set; }
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        public Guid? UnitId { get; set; }
        public string? UnitName { get; set; }
        public string? CreatedByName { get; set; }
        public Guid? ProjectId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public bool IsBlocked { get; set; }
        public List<BlockingTaskDto> BlockingTasks { get; set; } = new();
        public List<AssignmentWorkloadDto> WorkloadWarnings { get; set; } = new();
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
        public Guid? RelatedEntityId { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
