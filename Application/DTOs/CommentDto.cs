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
        [NotEmptyGuid(ErrorMessage = "TaskId khong duoc rong.")]
        public Guid TaskId { get; set; }

        [Required(ErrorMessage = "Noi dung binh luan khong duoc de trong.")]
        [MaxLength(1000, ErrorMessage = "Noi dung binh luan toi da 1000 ky tu.")]
        public string Content { get; set; } = string.Empty;
    }
}
