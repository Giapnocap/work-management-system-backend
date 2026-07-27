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
        [NotEmptyGuid(ErrorMessage = "TaskId khong duoc rong.")]
        public Guid TaskId { get; set; }

        [Required(ErrorMessage = "Ten cong viec con khong duoc de trong.")]
        [MaxLength(200, ErrorMessage = "Ten cong viec con toi da 200 ky tu.")]
        public string Title { get; set; } = string.Empty;
    }
}
