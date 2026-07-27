using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class MemberDto
    {
        [NotEmptyGuid(ErrorMessage = "UserId khong duoc rong.")]
        public Guid UserId { get; set; }
    }
}
