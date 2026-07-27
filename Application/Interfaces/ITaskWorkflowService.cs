using WorkManagementSystem.Domain.Entities;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskWorkflowService
    {
        Task<List<Guid>> ResolveTaskRecipients(Guid taskId, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetExpectedUserIds(Guid taskId, CancellationToken cancellationToken = default);
        Task ApplyCompletionStateAsync(TaskItem task, Guid currentUserId, CancellationToken cancellationToken = default);
    }
}
