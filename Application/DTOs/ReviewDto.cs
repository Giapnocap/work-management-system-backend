using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class ReviewDto
    {
        [NotEmptyGuid(ErrorMessage = "ProgressId khong duoc rong.")]
        public Guid ProgressId { get; set; }
        public bool Approve { get; set; }

        [MaxLength(1000, ErrorMessage = "Ghi chu duyet toi da 1000 ky tu.")]
        public string Comment { get; set; } = string.Empty;
    }
}
