using WorkManagementSystem.Application.DTOs;

namespace WorkManagementSystem.Application.Interfaces;

public interface ITaskRealtimeNotifier
{
    Task CommentAddedAsync(Guid taskId, CommentDto comment, CancellationToken cancellationToken = default);
    Task ReactionChangedAsync(Guid taskId, Guid commentId, CancellationToken cancellationToken = default);
    Task CommentsSeenAsync(Guid taskId, Guid userId, CancellationToken cancellationToken = default);
    Task SubTaskAddedAsync(Guid taskId, SubTaskDto subTask, CancellationToken cancellationToken = default);
    Task SubTaskToggledAsync(Guid taskId, Guid subTaskId, bool isCompleted, CancellationToken cancellationToken = default);
    Task SubTaskDeletedAsync(Guid taskId, Guid subTaskId, CancellationToken cancellationToken = default);
}
