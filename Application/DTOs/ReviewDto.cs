using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class ReviewDto
    {
        [NotEmptyGuid(ErrorMessage = "ProgressId không được rỗng.")]
        public Guid ProgressId { get; set; }
        public bool Approve { get; set; }

        [MaxLength(1000, ErrorMessage = "Ghi chú duyệt tối đa 1000 ký tự.")]
        public string Comment { get; set; } = string.Empty;
    }
}
