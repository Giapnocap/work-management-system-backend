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
        public string Priority { get; set; } = "Medium";
        public bool RequiresReview { get; set; } = true;
        public Guid? ProjectId { get; set; }
    }

    public class UpdateTaskDto
    {
        [Required(ErrorMessage = "Tieu de khong duoc de trong!")]
        [MaxLength(200, ErrorMessage = "Tieu de toi da 200 ky tu!")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Mo ta toi da 1000 ky tu!")]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
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
    }
}
