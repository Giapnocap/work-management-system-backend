using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ICommentService
    {
        Task<CommentDto> AddComment(CreateCommentDto dto, Guid userId, CancellationToken cancellationToken = default);
        Task<List<CommentDto>> GetComments(Guid taskId, Guid? userId = null, CancellationToken cancellationToken = default);
        Task Delete(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
        Task<Guid> ToggleReaction(Guid commentId, Guid userId, string emoji, CancellationToken cancellationToken = default);
        Task MarkAsSeen(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
    }
}
