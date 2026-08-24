using WorkManagementSystem.Domain.Entities;
using WorkManagementSystem.Domain.Enums;

namespace WorkManagementSystem.Application.Interfaces
{
    public interface ITaskWorkflowService
    {
        Task<List<Guid>> ResolveTaskRecipients(Guid taskId, CancellationToken cancellationToken = default);
        Task<List<Guid>> GetExpectedUserIds(Guid taskId, CancellationToken cancellationToken = default);
        Task EnsureDependenciesCompletedAsync(Guid taskId, CancellationToken cancellationToken = default);
        ProgressStatus ResolveNewProgressStatus(int percent, bool requiresReview);
        Task ApplyProgressReportAsync(
            TaskItem task,
            Progress progress,
            Guid reporterId,
            bool hasPendingSubmittedProgress,
            CancellationToken cancellationToken = default);
        Task ApplyReviewDecisionAsync(
            TaskItem task,
            Progress progress,
            bool approve,
            Guid reviewerId,
            bool hasOtherSubmittedProgress,
            string? reason,
            CancellationToken cancellationToken = default);
        Task ApplyCompletionStateAsync(TaskItem task, Guid currentUserId, CancellationToken cancellationToken = default);
    }
}
