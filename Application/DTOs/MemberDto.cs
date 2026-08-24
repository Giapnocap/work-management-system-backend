using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class MemberDto
    {
        [NotEmptyGuid(ErrorMessage = "UserId không được rỗng.")]
        public Guid UserId { get; set; }
    }
}
