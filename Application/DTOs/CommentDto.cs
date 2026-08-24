using System;
using System.ComponentModel.DataAnnotations;
using WorkManagementSystem.Application.Validation;

namespace WorkManagementSystem.Application.DTOs
{
    public class CommentDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? UserFullName { get; set; }
        public string? UserEmployeeCode { get; set; }
        public List<ReactionSummaryDto> Reactions { get; set; } = new();
        public List<string> SeenByUserFullNames { get; set; } = new();
        public string? MyReaction { get; set; }
    }

    public class ReactionSummaryDto
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<string> UserFullNames { get; set; } = new();
    }

    public class CreateCommentDto
    {
        [NotEmptyGuid(ErrorMessage = "TaskId không được rỗng.")]
        public Guid TaskId { get; set; }

        [Required(ErrorMessage = "Nội dung bình luận không được để trống.")]
        [MaxLength(1000, ErrorMessage = "Nội dung bình luận tối đa 1000 ký tự.")]
        public string Content { get; set; } = string.Empty;
    }
}
