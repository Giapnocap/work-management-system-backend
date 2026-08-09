using Microsoft.AspNetCore.SignalR;
using WorkManagementSystem.Application.DTOs;
using WorkManagementSystem.Application.Interfaces;

namespace WorkManagementSystem.API.Hubs;

public sealed class SignalRTaskRealtimeNotifier : ITaskRealtimeNotifier
{
    private readonly IHubContext<DiscussionHub> _hubContext;
    private readonly ILogger<SignalRTaskRealtimeNotifier> _logger;

    public SignalRTaskRealtimeNotifier(
        IHubContext<DiscussionHub> hubContext,
        ILogger<SignalRTaskRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task CommentAddedAsync(
        Guid taskId,
        CommentDto comment,
        CancellationToken cancellationToken = default)
        => SendAsync(taskId, "ReceiveComment", new object?[] { comment }, cancellationToken);

    public Task ReactionChangedAsync(
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken = default)
        => SendAsync(taskId, "UpdateReaction", new object?[] { commentId }, cancellationToken);

    public Task CommentsSeenAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => SendAsync(taskId, "UpdateSeen", new object?[] { userId }, cancellationToken);

    public Task SubTaskAddedAsync(
        Guid taskId,
        SubTaskDto subTask,
        CancellationToken cancellationToken = default)
        => SendAsync(taskId, "ReceiveSubTaskAdded", new object?[] { subTask }, cancellationToken);

    public Task SubTaskToggledAsync(
        Guid taskId,
        Guid subTaskId,
        bool isCompleted,
        CancellationToken cancellationToken = default)
        => SendAsync(
            taskId,
            "ReceiveSubTaskToggled",
            new object?[] { subTaskId, isCompleted },
            cancellationToken);

    public Task SubTaskDeletedAsync(
        Guid taskId,
        Guid subTaskId,
        CancellationToken cancellationToken = default)
        => SendAsync(taskId, "ReceiveSubTaskDeleted", new object?[] { subTaskId }, cancellationToken);

    private async Task SendAsync(
        Guid taskId,
        string method,
        object?[] arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .Group(TaskDiscussionGroup.For(taskId))
                .SendCoreAsync(method, arguments, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Realtime notification {Method} was cancelled for task {TaskId}.",
                method,
                taskId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Realtime notification {Method} failed for task {TaskId}.",
                method,
                taskId);
        }
    }
}
