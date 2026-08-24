using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class SubTaskDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSubTaskDto
    {
        [NotEmptyGuid(ErrorMessage = "TaskId không được rỗng.")]
        public Guid TaskId { get; set; }

        [Required(ErrorMessage = "Tên công việc con không được để trống.")]
        [MaxLength(200, ErrorMessage = "Tên công việc con tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;
    }
}
